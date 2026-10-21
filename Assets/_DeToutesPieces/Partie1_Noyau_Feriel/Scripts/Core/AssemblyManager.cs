using System;
using System.Collections.Generic;
using DeToutesPieces.Feedback;
using UnityEngine;

namespace DeToutesPieces.Core
{
    /// <summary>
    /// Chef d'orchestre du montage : enchaîne les étapes de la notice, vérifie leurs conditions,
    /// pilote le guidage, l'annulation, les indices progressifs et les mesures de session.
    /// N'effectue jamais le montage à la place de l'utilisateur.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class AssemblyManager : MonoBehaviour
    {
        public static AssemblyManager Instance { get; private set; }

        public enum HintLevel { None = 0, Text = 1, Highlight = 2, Demonstration = 3 }

        [SerializeField] private List<AssemblyStep> steps = new List<AssemblyStep>();
        [SerializeField] private bool startOnPlay = true;

        [Tooltip("Si vrai, l'utilisateur confirme avec « Étape suivante » (document V1). Sinon passage automatique.")]
        [SerializeField] private bool requireConfirmToAdvance = true;

        public IReadOnlyList<AssemblyStep> Steps => steps;
        public int CurrentStepIndex { get; private set; } = -1;
        public AssemblyStep CurrentStep => CurrentStepIndex >= 0 && CurrentStepIndex < steps.Count ? steps[CurrentStepIndex] : null;
        public bool IsCurrentStepReady { get; private set; }
        public bool IsFinished { get; private set; }
        public bool IsPaused { get; private set; }
        public HintLevel CurrentHintLevel { get; private set; }
        public SessionStats Stats { get; } = new SessionStats();

        public event Action<int, AssemblyStep> StepStarted;
        public event Action<int, AssemblyStep> StepProgressChanged;
        public event Action<int, AssemblyStep> StepReady;
        public event Action<int, AssemblyStep> StepCompleted;
        public event Action AssemblyCompleted;
        public event Action<StepRequirement> GuidanceTargetChanged;
        public event Action<HintLevel, string, StepRequirement> HintRequested;
        public event Action<bool> PauseChanged;

        private readonly Stack<IUndoableAction> undoStack = new Stack<IUndoableAction>();
        private StepRequirement currentGuidance;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool initialized;

        private void Start()
        {
            Initialize();
            if (startOnPlay && steps.Count > 0 && CurrentStepIndex < 0) BeginStep(0);
        }

        /// <summary>Abonnement aux conditions des étapes (idempotent).</summary>
        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            foreach (var step in steps)
                foreach (var req in step.requirements)
                    if (req != null)
                    {
                        req.StateChanged += OnRequirementChanged;
                        req.SetActiveForStep(false);
                    }
        }

        /// <summary>
        /// Lance le montage (appelé par le déroulement de session). Avec fromStep &gt; 0, reprend au dernier jalon :
        /// les étapes précédentes sont reconstituées automatiquement, sans retours ni mesures.
        /// </summary>
        public void StartAssembly(int fromStep = 0)
        {
            Initialize();
            if (steps.Count == 0) return;
            fromStep = Mathf.Clamp(fromStep, 0, steps.Count - 1);
            Stats.Reset();
            if (fromStep == 0) { BeginStep(0); return; }

            FeedbackBus.Muted = true;
            try
            {
                BeginStep(0);
                while (CurrentStepIndex < fromStep && !IsFinished)
                {
                    foreach (var r in CurrentStep.requirements)
                        if (r != null && !r.IsSatisfied) r.TryAutoComplete();
                    if (!IsCurrentStepReady) break; // reconstitution impossible : on reprend ici
                    ConfirmNextStep();
                }
            }
            finally
            {
                FeedbackBus.Muted = false;
            }
            // La reconstitution ne compte ni dans le temps actif ni dans les étapes réussies sans aide.
            Stats.activeTimeSeconds = 0f;
            Stats.stepsCompletedWithoutHint.Clear();
            Stats.OnStepStarted();
            StepStarted?.Invoke(CurrentStepIndex, CurrentStep); // rafraîchit l'interface après la reprise
        }

        private void Update()
        {
            if (!IsPaused && !IsFinished && CurrentStepIndex >= 0) Stats.Tick(Time.unscaledDeltaTime);
        }

        // ---------------- Étapes ----------------

        public void BeginStep(int index)
        {
            if (index < 0 || index >= steps.Count) return;

            if (CurrentStep != null)
                foreach (var r in CurrentStep.requirements) if (r != null) r.SetActiveForStep(false);

            CurrentStepIndex = index;
            IsCurrentStepReady = false;
            CurrentHintLevel = HintLevel.None;
            undoStack.Clear(); // l'annulation est limitée à l'étape en cours ; les étapes validées restent acquises.
            Stats.OnStepStarted();

            foreach (var r in CurrentStep.requirements) if (r != null) r.SetActiveForStep(true);

            StepStarted?.Invoke(index, CurrentStep);
            Evaluate(false);
        }

        /// <summary>Bouton « Étape suivante ». Refuse tant que l'étape n'est pas complète.</summary>
        public bool ConfirmNextStep()
        {
            if (IsFinished || CurrentStep == null) return false;
            if (!IsCurrentStepReady)
            {
                FeedbackBus.Emit(FeedbackKind.WrongAction, null,
                    $"Étape incomplète : {CurrentStep.SatisfiedCount}/{CurrentStep.RequirementCount} opérations réalisées.");
                return false;
            }

            int finished = CurrentStepIndex;
            Stats.OnStepCompleted(finished);
            StepCompleted?.Invoke(finished, CurrentStep);

            if (finished + 1 < steps.Count)
            {
                FeedbackBus.Emit(FeedbackKind.StepCompleted, null, $"Étape {finished + 1} validée.");
                BeginStep(finished + 1);
            }
            else
            {
                foreach (var r in CurrentStep.requirements) if (r != null) r.SetActiveForStep(false);
                IsFinished = true;
                SetGuidance(null);
                FeedbackBus.Emit(FeedbackKind.AssemblyCompleted, null, "Montage terminé ! Rappel de la notice : charge maximale 10 kg, ne pas s'asseoir sur la table.");
                AssemblyCompleted?.Invoke();
            }
            return true;
        }

        private void OnRequirementChanged(StepRequirement req)
        {
            if (CurrentStep == null || !CurrentStep.requirements.Contains(req)) return;
            Evaluate(true);
        }

        private void Evaluate(bool notify)
        {
            var step = CurrentStep;
            if (step == null) return;

            StepProgressChanged?.Invoke(CurrentStepIndex, step);
            SetGuidance(step.FirstUnsatisfied);

            bool complete = step.IsComplete;
            if (complete && !IsCurrentStepReady)
            {
                IsCurrentStepReady = true;
                if (notify)
                    FeedbackBus.Emit(FeedbackKind.StepReady, null,
                        requireConfirmToAdvance ? "Étape terminée. Appuyez sur « Étape suivante »." : "Étape terminée.");
                StepReady?.Invoke(CurrentStepIndex, step);
                if (!requireConfirmToAdvance) ConfirmNextStep();
            }
            else if (!complete && IsCurrentStepReady)
            {
                IsCurrentStepReady = false; // suite à une annulation
            }
        }

        /// <summary>
        /// Publie une cible de guidage hors des étapes de montage (prise en main, inventaire).
        /// La flèche, le guide de rotation et le support tournant réagissent comme pendant le montage.
        /// </summary>
        public void PublishGuidance(StepRequirement req) => SetGuidance(req);

        private void SetGuidance(StepRequirement req)
        {
            if (req == currentGuidance) return;
            currentGuidance = req;
            GuidanceTargetChanged?.Invoke(req);
        }

        public StepRequirement CurrentGuidance => currentGuidance;

        // ---------------- Annulation ----------------

        public void RegisterAction(IUndoableAction action)
        {
            if (action != null) undoStack.Push(action);
        }

        public bool CanUndo => undoStack.Count > 0;

        /// <summary>Bouton B : retire la dernière action réversible de l'étape en cours.</summary>
        public bool Undo()
        {
            if (undoStack.Count == 0)
            {
                FeedbackBus.Emit(FeedbackKind.Info, null, "Aucune action à annuler dans cette étape.");
                return false;
            }
            var a = undoStack.Pop();
            a.Undo();
            Stats.undoCount++;
            FeedbackBus.Emit(FeedbackKind.UndoPerformed, null, $"Annulé : {a.Label}.");
            return true;
        }

        // ---------------- Aides ----------------

        /// <summary>Bouton A : indice progressif (texte → surbrillance → démonstration).</summary>
        public void RequestHint()
        {
            var step = CurrentStep;
            if (step == null || IsFinished) return;

            if (CurrentHintLevel < HintLevel.Demonstration) CurrentHintLevel++;
            Stats.hintsRequested++;

            string text;
            int i = (int)CurrentHintLevel - 1;
            if (i < step.textHints.Count && !string.IsNullOrEmpty(step.textHints[i])) text = step.textHints[i];
            else if (currentGuidance != null) text = $"Prochaine opération : {currentGuidance.Label}.";
            else text = step.instruction;

            HintRequested?.Invoke(CurrentHintLevel, text, currentGuidance);
            FeedbackBus.Emit(FeedbackKind.Info, null, text);
        }

        public void ReportPlacementError() => Stats.placementErrors++;
        public void ReportFasteningError() => Stats.fasteningErrors++;
        public void ReportPartReturned() => Stats.partsReturned++;

        // ---------------- Pause ----------------

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            PauseChanged?.Invoke(paused);
        }
    }
}
