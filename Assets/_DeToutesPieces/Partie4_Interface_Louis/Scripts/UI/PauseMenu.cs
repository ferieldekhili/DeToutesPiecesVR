using DeToutesPieces.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Menu pause et réglages (bouton Menu de la main gauche, touche Échap) :
    /// reprise, volume, vibrations, taille des textes, posture, main dominante, redémarrage, sortie.
    /// Le temps actif de la session est suspendu pendant la pause.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private float distance = 0.7f;
        [SerializeField] private float heightOffset = -0.05f;

        private RectTransform canvas;
        private TMP_Text volumeValue, hapticsLabel, textValue, postureLabel, handLabel;

        private void Start()
        {
            Build();
            canvas.gameObject.SetActive(false);
            if (AssemblyManager.Instance != null) AssemblyManager.Instance.PauseChanged += OnPause;
            SessionSettings.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (AssemblyManager.Instance != null) AssemblyManager.Instance.PauseChanged -= OnPause;
            SessionSettings.Changed -= Refresh;
        }

        private void OnPause(bool paused)
        {
            canvas.gameObject.SetActive(paused);
            if (!paused) return;
            RuntimeUI.PlaceInFrontOfUser(transform, distance, heightOffset);
            Refresh();
        }

        private void Build()
        {
            canvas = RuntimeUI.CreateWorldCanvas(transform, "Menu pause", new Vector2(640, 640));
            RuntimeUI.Text(canvas, "Pause", 40, new Vector2(30, 20), new Vector2(580, 56), RuntimeUI.CommandYellow, FontStyles.Bold);
            RuntimeUI.Button(canvas, "Reprendre", new Vector2(30, 84), new Vector2(580, 60), RuntimeUI.CommandYellow, RuntimeUI.TextDark,
                () => AssemblyManager.Instance?.SetPaused(false), 24);

            float y = 166f;
            volumeValue = Row("Volume", ref y, () => SessionSettings.Volume -= 0.1f, () => SessionSettings.Volume += 0.1f);
            textValue = Row("Taille des textes", ref y, () => SessionSettings.TextScale -= 0.1f, () => SessionSettings.TextScale += 0.1f);

            hapticsLabel = Toggle("Vibrations", ref y, () => SessionSettings.Haptics = !SessionSettings.Haptics);
            postureLabel = Toggle("Posture", ref y, () => SessionSettings.Posture =
                SessionSettings.Posture == WorkPosture.Standing ? WorkPosture.Seated : WorkPosture.Standing);
            handLabel = Toggle("Main dominante", ref y, () => SessionSettings.DominantHand =
                SessionSettings.DominantHand == Handedness.Right ? Handedness.Left : Handedness.Right);

            RuntimeUI.Button(canvas, "Recommencer", new Vector2(30, 560), new Vector2(280, 56), RuntimeUI.Neutral, Color.white,
                () => SessionFlow.Instance?.Restart());
            RuntimeUI.Button(canvas, "Quitter", new Vector2(330, 560), new Vector2(280, 56), RuntimeUI.Neutral, Color.white,
                () => SessionFlow.Instance?.Quit());
        }

        private TMP_Text Row(string label, ref float y, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
        {
            RuntimeUI.Text(canvas, label, 22, new Vector2(30, y + 10), new Vector2(260, 40), new Color(0.8f, 0.88f, 1f), FontStyles.Bold);
            RuntimeUI.Button(canvas, "–", new Vector2(300, y), new Vector2(70, 52), RuntimeUI.Neutral, Color.white, minus, 28);
            var value = RuntimeUI.Text(canvas, "", 22, new Vector2(380, y + 10), new Vector2(150, 40), Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            RuntimeUI.Button(canvas, "+", new Vector2(540, y), new Vector2(70, 52), RuntimeUI.Neutral, Color.white, plus, 28);
            y += 64f;
            return value;
        }

        private TMP_Text Toggle(string label, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            RuntimeUI.Text(canvas, label, 22, new Vector2(30, y + 10), new Vector2(260, 40), new Color(0.8f, 0.88f, 1f), FontStyles.Bold);
            RuntimeUI.Button(canvas, "", new Vector2(300, y), new Vector2(310, 52), RuntimeUI.InfoBlue, Color.white, onClick, out var value);
            y += 64f;
            return value;
        }

        private void Refresh()
        {
            if (canvas == null) return;
            volumeValue.text = $"{Mathf.RoundToInt(SessionSettings.Volume * 100f)} %";
            textValue.text = $"{Mathf.RoundToInt(SessionSettings.TextScale * 100f)} %";
            hapticsLabel.text = SessionSettings.Haptics ? "Activées" : "Désactivées";
            postureLabel.text = SessionSettings.Posture == WorkPosture.Standing ? "Debout" : "Assis";
            handLabel.text = SessionSettings.DominantHand == Handedness.Right ? "Droite" : "Gauche";
        }
    }
}
