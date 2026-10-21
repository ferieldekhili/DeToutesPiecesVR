using System;
using DeToutesPieces.Feedback;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeToutesPieces.Core
{
    public enum SessionPhase { Accueil, PriseEnMain, Inventaire, Montage, Bilan }

    /// <summary>
    /// Déroulement d'une session (document V1, « Fonctionnalités et déroulement ») :
    /// Accueil → Prise en main → Inventaire → Montage (6 étapes de la notice) → Bilan.
    /// Les autres parties réagissent à PhaseChanged et publient leur consigne via SetPhaseInfo ;
    /// elles signalent la fin de leur phase par CompletePhase.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class SessionFlow : MonoBehaviour
    {
        public static SessionFlow Instance { get; private set; }

        [Tooltip("Faux pour démarrer directement le montage (tests).")]
        [SerializeField] private bool showWelcome = true;

        public SessionPhase Phase { get; private set; } = SessionPhase.Accueil;
        public event Action<SessionPhase> PhaseChanged;
        public event Action PhaseInfoChanged;

        public string PhaseTitle { get; private set; }
        public string PhaseInstruction { get; private set; }
        public string PhaseProgress { get; private set; }
        /// <summary>Vrai quand la tâche de la phase est terminée (le bouton affiche « Continuer » au lieu de « Passer »).</summary>
        public bool PhaseCanContinue { get; private set; }

        private int resumeStep = -1;
        private AssemblyManager manager;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            manager = AssemblyManager.Instance;
            if (manager != null)
            {
                manager.StepCompleted += (i, s) => ProgressSave.Save(i + 1);
                manager.AssemblyCompleted += OnAssemblyCompleted;
            }
            if (showWelcome) Enter(SessionPhase.Accueil);
            else Enter(SessionPhase.Montage);
        }

        // ---------------- Commandes (écran d'accueil, panneau, menus) ----------------

        public void StartSession(bool withTutorial)
        {
            resumeStep = -1;
            ProgressSave.Clear();
            Enter(withTutorial ? SessionPhase.PriseEnMain : SessionPhase.Inventaire);
        }

        public void ResumeSession()
        {
            resumeStep = ProgressSave.SavedStep;
            Enter(SessionPhase.Montage);
        }

        /// <summary>Termine (ou passe) la phase indiquée si c'est la phase en cours.</summary>
        public void CompletePhase(SessionPhase phase)
        {
            if (phase != Phase) return;
            switch (phase)
            {
                case SessionPhase.PriseEnMain:
                    SessionSettings.TutorialDone = true;
                    Enter(SessionPhase.Inventaire);
                    break;
                case SessionPhase.Inventaire:
                    Enter(SessionPhase.Montage);
                    break;
            }
        }

        public void Restart()
        {
            ProgressSave.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetPhaseInfo(string title, string instruction, string progress, bool canContinue)
        {
            PhaseTitle = title;
            PhaseInstruction = instruction;
            PhaseProgress = progress;
            PhaseCanContinue = canContinue;
            PhaseInfoChanged?.Invoke();
        }

        // ---------------- Interne ----------------

        private void Enter(SessionPhase phase)
        {
            Phase = phase;
            switch (phase)
            {
                case SessionPhase.Accueil:
                    SetPhaseInfo("Bienvenue", "Choisissez vos options sur l'écran d'accueil, devant vous, puis commencez.", "", false);
                    break;
                case SessionPhase.PriseEnMain:
                    SetPhaseInfo("Prise en main", "Familiarisez-vous avec les gestes sur les objets d'exercice.", "", false);
                    break;
                case SessionPhase.Inventaire:
                    SetPhaseInfo("Inventaire", "Repérez les éléments du kit.", "", false);
                    break;
                case SessionPhase.Montage:
                    SetPhaseInfo("Montage", "", "", false);
                    break;
                case SessionPhase.Bilan:
                    SetPhaseInfo("Bilan", "Le bilan de votre session est affiché devant vous.", "", false);
                    break;
            }
            PhaseChanged?.Invoke(phase);

            if (phase == SessionPhase.Montage && manager != null)
            {
                if (resumeStep > 0) FeedbackBus.Emit(FeedbackKind.Info, null, $"Reprise au dernier jalon validé : étape {resumeStep + 1}.");
                manager.StartAssembly(Mathf.Max(0, resumeStep));
            }
        }

        private void OnAssemblyCompleted()
        {
            ProgressSave.Clear();
            Enter(SessionPhase.Bilan);
        }
    }
}
