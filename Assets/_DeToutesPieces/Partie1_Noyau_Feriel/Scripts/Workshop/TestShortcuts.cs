using DeToutesPieces.Core;
using DeToutesPieces.Feedback;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeToutesPieces.Workshop
{
    /// <summary>
    /// Raccourcis de test, actifs uniquement dans l'éditeur Unity (jamais dans le casque) :
    /// F5 = réaliser automatiquement l'opération désignée par la flèche,
    /// F6 = terminer automatiquement l'étape ou la phase en cours,
    /// F8 = effacer les réglages et la progression puis relancer (première utilisation).
    /// Ils servent à atteindre rapidement la partie à tester au XR Interaction Simulator.
    /// </summary>
    public class TestShortcuts : MonoBehaviour
    {
        private void Awake()
        {
            if (!Application.isEditor) enabled = false;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f5Key.wasPressedThisFrame) CompleteCurrentOperation();
            if (kb.f6Key.wasPressedThisFrame) CompleteCurrentStep();
            if (kb.f8Key.wasPressedThisFrame) ResetAndRestart();
        }

        private static void CompleteCurrentOperation()
        {
            var flow = SessionFlow.Instance;
            var m = AssemblyManager.Instance;
            if (flow != null && flow.Phase == SessionPhase.Inventaire) { flow.CompletePhase(SessionPhase.Inventaire); return; }
            var req = m != null ? m.CurrentGuidance : null;
            if (req == null) { FeedbackBus.Emit(FeedbackKind.Info, null, "[Test] Aucune opération en cours."); return; }
            bool ok = req.TryAutoComplete();
            FeedbackBus.Emit(FeedbackKind.Info, null, ok ? $"[Test] Réalisé automatiquement : {req.Label}." : $"[Test] Impossible de réaliser : {req.Label}.");
        }

        private static void CompleteCurrentStep()
        {
            var flow = SessionFlow.Instance;
            var m = AssemblyManager.Instance;
            if (flow != null && (flow.Phase == SessionPhase.PriseEnMain || flow.Phase == SessionPhase.Inventaire))
            {
                flow.CompletePhase(flow.Phase);
                return;
            }
            if (m == null || m.CurrentStep == null || m.IsFinished) return;
            for (int guard = 0; guard < 40 && !m.IsCurrentStepReady; guard++)
            {
                var req = m.CurrentGuidance;
                if (req == null || !req.TryAutoComplete()) break;
            }
            if (m.IsCurrentStepReady) m.ConfirmNextStep();
        }

        private static void ResetAndRestart()
        {
            foreach (var k in new[] { "main", "posture", "volume", "vibrations", "texte", "tutoriel" })
                PlayerPrefs.DeleteKey("dtp.reglages." + k);
            ProgressSave.Clear();
            PlayerPrefs.Save();
            if (SessionFlow.Instance != null) SessionFlow.Instance.Restart();
        }
    }
}
