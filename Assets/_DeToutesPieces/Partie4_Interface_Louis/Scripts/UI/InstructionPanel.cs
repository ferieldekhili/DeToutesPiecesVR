using System.Collections;
using DeToutesPieces.Core;
using DeToutesPieces.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Panneau de consigne latéral (ACV Communiquer / Observer) : étape courante, consigne courte,
    /// progression, dernier message de retour, et commandes Indice / Annuler / Étape suivante.
    /// Les couleurs sont toujours doublées d'un texte (accessibilité).
    /// </summary>
    public class InstructionPanel : MonoBehaviour
    {
        [Header("Textes")]
        [SerializeField] private TMP_Text stepTitle;
        [SerializeField] private TMP_Text instruction;
        [SerializeField] private TMP_Text progress;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Image messageBackground;

        [Header("Boutons")]
        [SerializeField] private Button hintButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonLabel;

        [SerializeField] private float messageDuration = 5f;

        private Coroutine messageRoutine;
        private AssemblyManager manager;

        private void OnEnable()
        {
            manager = AssemblyManager.Instance;
            if (hintButton) hintButton.onClick.AddListener(OnHint);
            if (undoButton) undoButton.onClick.AddListener(OnUndo);
            if (nextButton) nextButton.onClick.AddListener(OnNext);
            Subscribe();
        }

        private void Start()
        {
            if (manager == null) { manager = AssemblyManager.Instance; Subscribe(); }
            RefreshAll();
        }

        private void OnDisable()
        {
            if (hintButton) hintButton.onClick.RemoveListener(OnHint);
            if (undoButton) undoButton.onClick.RemoveListener(OnUndo);
            if (nextButton) nextButton.onClick.RemoveListener(OnNext);
            Unsubscribe();
        }

        private bool subscribed;

        private void Subscribe()
        {
            if (subscribed) return;
            if (manager != null)
            {
                manager.StepStarted += OnStepChanged;
                manager.StepProgressChanged += OnStepChanged;
                manager.StepReady += OnStepChanged;
                manager.AssemblyCompleted += RefreshAll;
                subscribed = true;
            }
            if (FeedbackService.Instance != null) FeedbackService.Instance.MessageRaised += OnMessage;
        }

        private void Unsubscribe()
        {
            if (manager != null && subscribed)
            {
                manager.StepStarted -= OnStepChanged;
                manager.StepProgressChanged -= OnStepChanged;
                manager.StepReady -= OnStepChanged;
                manager.AssemblyCompleted -= RefreshAll;
            }
            subscribed = false;
            if (FeedbackService.Instance != null) FeedbackService.Instance.MessageRaised -= OnMessage;
        }

        private void OnStepChanged(int index, AssemblyStep step) => RefreshAll();

        private void RefreshAll()
        {
            if (manager == null) return;

            if (manager.IsFinished)
            {
                Set(stepTitle, "Montage terminé");
                Set(instruction,
                    $"Temps actif : {manager.Stats.FormatActiveTime()}\n" +
                    $"Erreurs de pose : {manager.Stats.placementErrors + manager.Stats.fasteningErrors}\n" +
                    $"Aides demandées : {manager.Stats.hintsRequested}\n\n" +
                    "La notice IKEA reste la référence pour le montage réel.");
                Set(progress, "");
                SetNext(false, "Terminé");
                return;
            }

            var step = manager.CurrentStep;
            if (step == null) return;

            Set(stepTitle, $"Étape {manager.CurrentStepIndex + 1}/{manager.Steps.Count} — {step.title}");
            Set(instruction, step.instruction + (step.noticePage > 0 ? $"\n<size=70%><color=#8FB8FF>Notice p. {step.noticePage}</color></size>" : ""));

            var sb = new System.Text.StringBuilder();
            if (step.RequirementCount <= 6)
            {
                foreach (var r in step.requirements)
                {
                    if (r == null) continue;
                    sb.Append(Box(r.IsSatisfied));
                    sb.AppendLine(r.Label);
                }
            }
            else
            {
                // Étape longue (ex. serrage des 16 vis) : résumé par type d'opération + opération suivante.
                var groups = new System.Collections.Generic.List<string>();
                var done = new System.Collections.Generic.Dictionary<string, int>();
                var total = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var r in step.requirements)
                {
                    if (r == null) continue;
                    string key = GroupName(r);
                    if (!total.ContainsKey(key)) { groups.Add(key); total[key] = 0; done[key] = 0; }
                    total[key]++;
                    if (r.IsSatisfied) done[key]++;
                }
                foreach (var g in groups)
                    sb.AppendLine($"{Box(done[g] == total[g])}{g} : {done[g]}/{total[g]}");
                var next = step.FirstUnsatisfied;
                if (next != null) sb.AppendLine($"<color=#FFCC1A>Suivant :</color> {next.Label}");
            }
            sb.Append($"<b>{step.SatisfiedCount}/{step.RequirementCount}</b> opérations");
            Set(progress, sb.ToString());

            SetNext(manager.IsCurrentStepReady, manager.CurrentStepIndex + 1 < manager.Steps.Count ? "Étape suivante" : "Terminer");
        }

        private void SetNext(bool interactable, string label)
        {
            if (nextButton) nextButton.interactable = interactable;
            Set(nextButtonLabel, label);
        }

        private void OnMessage(FeedbackKind kind, string text, Color color)
        {
            Set(message, text);
            if (messageBackground) { var c = color; c.a = 0.35f; messageBackground.color = c; messageBackground.enabled = true; }
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(ClearMessageLater());
        }

        private IEnumerator ClearMessageLater()
        {
            yield return new WaitForSeconds(messageDuration);
            Set(message, "");
            if (messageBackground) messageBackground.enabled = false;
        }

        private void OnHint() => manager?.RequestHint();
        private void OnUndo() => manager?.Undo();
        private void OnNext() => manager?.ConfirmNextStep();

        private static string Box(bool ok) => ok ? "<color=#3CD95A><b>[OK]</b></color> " : "<color=#FFCC1A><b>[  ]</b></color> ";

        private static string GroupName(StepRequirement r)
        {
            switch (r.GetType().Name)
            {
                case "GhostSnapTarget": return "Pièces positionnées";
                case "ScrewSlot": return "Vis engagées";
                case "TightenRequirement": return "Vis serrées";
                case "GazeCheckRequirement": return "Contrôle visuel";
                default: return "Opérations";
            }
        }

        private static void Set(TMP_Text t, string s)
        {
            if (t != null) t.text = s;
        }
    }
}
