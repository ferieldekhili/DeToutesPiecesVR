using DeToutesPieces.Core;
using TMPro;
using UnityEngine;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Écran de bilan (phase Bilan) : temps actif, erreurs, aides utilisées, sans note ni classement
    /// (document V1), rappels de sécurité de la notice et choix de recommencer ou quitter.
    /// </summary>
    public class SummaryScreen : MonoBehaviour
    {
        [SerializeField] private float distance = 0.75f;
        [SerializeField] private float heightOffset = -0.05f;

        private RectTransform canvas;
        private TMP_Text body;

        private void Start()
        {
            canvas = RuntimeUI.CreateWorldCanvas(transform, "Écran de bilan", new Vector2(760, 640));
            RuntimeUI.Text(canvas, "Bilan de la session", 40, new Vector2(30, 20), new Vector2(700, 56), RuntimeUI.CommandYellow, FontStyles.Bold);
            body = RuntimeUI.Text(canvas, "", 23, new Vector2(30, 88), new Vector2(700, 450), Color.white);
            RuntimeUI.Button(canvas, "Recommencer", new Vector2(30, 560), new Vector2(340, 60), RuntimeUI.CommandYellow, RuntimeUI.TextDark,
                () => SessionFlow.Instance?.Restart(), 24);
            RuntimeUI.Button(canvas, "Quitter", new Vector2(390, 560), new Vector2(340, 60), RuntimeUI.Neutral, Color.white,
                () => SessionFlow.Instance?.Quit(), 24);
            canvas.gameObject.SetActive(false);

            if (SessionFlow.Instance != null) SessionFlow.Instance.PhaseChanged += OnPhase;
        }

        private void OnDestroy()
        {
            if (SessionFlow.Instance != null) SessionFlow.Instance.PhaseChanged -= OnPhase;
        }

        private void OnPhase(SessionPhase phase)
        {
            bool show = phase == SessionPhase.Bilan;
            canvas.gameObject.SetActive(show);
            if (!show) return;
            RuntimeUI.PlaceInFrontOfUser(transform, distance, heightOffset);
            Fill();
        }

        private void Fill()
        {
            var m = AssemblyManager.Instance;
            if (m == null) return;
            var s = m.Stats;
            int steps = m.Steps.Count;
            body.text =
                $"Montage terminé : {steps}/{steps} étapes de la notice.\n\n" +
                $"<color=#8FB8FF>Temps actif</color>   {s.FormatActiveTime()}\n" +
                $"<color=#8FB8FF>Étapes réussies sans aide</color>   {s.stepsCompletedWithoutHint.Count}/{steps}\n" +
                $"<color=#8FB8FF>Erreurs de positionnement</color>   {s.placementErrors}\n" +
                $"<color=#8FB8FF>Erreurs de vissage</color>   {s.fasteningErrors}\n" +
                $"<color=#8FB8FF>Aides demandées</color>   {s.hintsRequested}\n" +
                $"<color=#8FB8FF>Annulations</color>   {s.undoCount}\n" +
                $"<color=#8FB8FF>Pièces remises à leur place</color>   {s.partsReturned}\n\n" +
                "<size=85%>Aucune note ni classement : ces mesures servent à repérer ce qui reste difficile.\n" +
                "<color=#FFCC1A>Rappel de la notice : charge maximale 10 kg, ne pas s'asseoir sur la table.</color>\n" +
                "Pour le montage réel, la notice IKEA AA-2547698-1 reste la référence.</size>";
        }
    }
}
