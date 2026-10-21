using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Feedback;
using DeToutesPieces.Parts;
using UnityEngine;

namespace DeToutesPieces.Interaction
{
    /// <summary>
    /// Prise en main (document V1, section 5 « plan d'apprentissage ») : avant le montage, l'utilisateur
    /// pratique chaque geste sur des objets d'exercice qui ne font pas partie du kit :
    /// saisir, tourner et poser un cube sur sa silhouette, engager une vis, la serrer à la clé Allen.
    /// Chaque geste est validé immédiatement ; la flèche de guidage et les retours sont les mêmes qu'au montage.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [SerializeField] private GameObject practiceRoot;
        [SerializeField] private GhostSnapTarget practiceTarget;
        [SerializeField] private ScrewSlot practiceSlot;
        [SerializeField] private TightenRequirement practiceTighten;

        private class Task
        {
            public StepRequirement req;
            public string title;
            public string instruction;
        }

        private readonly List<Task> tasks = new List<Task>();
        private int index = -1;
        private bool running;
        private SessionFlow flow;

        private void Awake()
        {
            if (practiceRoot != null) practiceRoot.SetActive(false);
        }

        private void Start()
        {
            tasks.Add(new Task
            {
                req = practiceTarget,
                title = "Saisir, tourner et poser",
                instruction = "Approchez la main du cube d'exercice et maintenez la gâchette latérale pour le saisir. " +
                              "Tournez-le pour qu'il corresponde à la silhouette blanche (elle devient verte), puis relâchez : il s'aligne tout seul."
            });
            tasks.Add(new Task
            {
                req = practiceSlot,
                title = "Engager une vis",
                instruction = "Prenez la vis d'exercice posée à côté du cube et lâchez-la près de l'anneau jaune désigné par la flèche : elle s'engage dans le trou."
            });
            tasks.Add(new Task
            {
                req = practiceTighten,
                title = "Serrer à la clé Allen",
                instruction = "Prenez la clé Allen (emplacement jaune, à gauche), approchez son embout court de la vis, dans son axe : elle s'emboîte. " +
                              "Tournez le poignet par petits quarts de tour dans le sens de la flèche circulaire. Vous pouvez relâcher et reprendre la clé."
            });

            flow = SessionFlow.Instance;
            if (flow != null)
            {
                flow.PhaseChanged += OnPhase;
                OnPhase(flow.Phase);
            }
        }

        private void OnDestroy()
        {
            if (flow != null) flow.PhaseChanged -= OnPhase;
        }

        private void OnPhase(SessionPhase phase)
        {
            if (phase == SessionPhase.PriseEnMain && !running) Begin();
            else if (phase != SessionPhase.PriseEnMain && running) End();
        }

        private void Begin()
        {
            running = true;
            if (practiceRoot != null) practiceRoot.SetActive(true);
            foreach (var t in tasks) if (t.req != null) t.req.StateChanged += OnRequirementChanged;
            index = -1;
            Next();
        }

        private void Next()
        {
            index++;
            if (index >= tasks.Count) { Done(); return; }
            var t = tasks[index];
            t.req.SetActiveForStep(true);
            AssemblyManager.Instance?.PublishGuidance(t.req);
            Publish(false);
        }

        private void OnRequirementChanged(StepRequirement r)
        {
            if (!running || index < 0 || index >= tasks.Count) return;
            if (r != tasks[index].req || !r.IsSatisfied) return;
            string t = tasks[index].title;
            FeedbackBus.Emit(FeedbackKind.StepReady, null, $"Geste réussi : {char.ToLowerInvariant(t[0]) + t.Substring(1)}.");
            Next();
        }

        private void Done()
        {
            AssemblyManager.Instance?.PublishGuidance(null);
            flow?.SetPhaseInfo("Prise en main terminée",
                "Vous savez saisir, orienter, poser, engager une vis et la serrer.\n" +
                "Rappels : <b>A</b> = indice, <b>B</b> = annuler, <b>X</b> = fenêtre d'outils, <b>Menu</b> = pause.\n" +
                "Appuyez sur « Continuer » pour passer à l'inventaire du kit.",
                ProgressText(), true);
        }

        private void Publish(bool done)
        {
            var t = tasks[index];
            flow?.SetPhaseInfo($"Prise en main {index + 1}/{tasks.Count} — {t.title}", t.instruction, ProgressText(), done);
        }

        private string ProgressText()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < tasks.Count; i++)
            {
                bool ok = tasks[i].req != null && tasks[i].req.IsSatisfied;
                sb.Append(ok ? "<color=#3CD95A><b>[OK]</b></color> " : "<color=#FFCC1A><b>[  ]</b></color> ");
                sb.AppendLine(tasks[i].title);
            }
            return sb.ToString();
        }

        private void End()
        {
            running = false;
            foreach (var t in tasks)
            {
                if (t.req == null) continue;
                t.req.StateChanged -= OnRequirementChanged;
                t.req.SetActiveForStep(false);
            }
            AssemblyManager.Instance?.PublishGuidance(null);

            // La clé Allen revient à son emplacement pour le montage (si elle n'est pas tenue).
            var key = FindFirstObjectByType<AllenKey>();
            if (key != null && key.TryGetComponent(out AssemblyPart kp) && kp.State != AssemblyPart.PartState.Held) kp.ReturnHome();

            if (practiceRoot != null) practiceRoot.SetActive(false);
        }

        /// <summary>Pour les tests automatisés : réalise le geste en cours.</summary>
        public bool AutoCompleteCurrent()
        {
            if (!running || index < 0 || index >= tasks.Count) return false;
            return tasks[index].req.TryAutoComplete();
        }
    }
}
