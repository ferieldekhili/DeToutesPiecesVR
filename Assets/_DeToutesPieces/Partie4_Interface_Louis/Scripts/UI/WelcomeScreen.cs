using DeToutesPieces.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Écran d'accueil (phase Accueil) : présentation, choix de la main dominante et de la posture,
    /// démarrage avec ou sans prise en main, reprise au dernier jalon sauvegardé.
    /// </summary>
    public class WelcomeScreen : MonoBehaviour
    {
        [SerializeField] private float distance = 0.75f;
        [SerializeField] private float heightOffset = -0.05f;

        private RectTransform canvas;
        private Button rightHand, leftHand, standing, seated, skipTutorial, resume;
        private TMP_Text resumeLabel;

        private void Start()
        {
            Build();
            var flow = SessionFlow.Instance;
            if (flow != null) flow.PhaseChanged += OnPhase;
            SessionSettings.Changed += RefreshOptions;
            OnPhase(flow != null ? flow.Phase : SessionPhase.Montage);
        }

        private void OnDestroy()
        {
            if (SessionFlow.Instance != null) SessionFlow.Instance.PhaseChanged -= OnPhase;
            SessionSettings.Changed -= RefreshOptions;
        }

        private void OnPhase(SessionPhase phase)
        {
            bool show = phase == SessionPhase.Accueil;
            canvas.gameObject.SetActive(show);
            if (!show) return;
            RuntimeUI.PlaceInFrontOfUser(transform, distance, heightOffset);
            RefreshOptions();
        }

        private void Build()
        {
            canvas = RuntimeUI.CreateWorldCanvas(transform, "Écran d'accueil", new Vector2(780, 630));
            RuntimeUI.Text(canvas, "De Toutes Pièces VR", 44, new Vector2(30, 24), new Vector2(540, 60), RuntimeUI.CommandYellow, FontStyles.Bold);
            AddSchoolLogo();
            RuntimeUI.Text(canvas, "Apprendre à monter la table de chevet IKEA KNARREVIK, étape par étape.\n" +
                                   "Durée indicative : 15 à 20 minutes. Les pièces, les vis et la clé se saisissent avec la gâchette latérale (VR) ou le clic gauche (clavier-souris).",
                20, new Vector2(30, 86), new Vector2(720, 100), Color.white);

            RuntimeUI.Text(canvas, "Main dominante", 22, new Vector2(30, 214), new Vector2(260, 40), new Color(0.8f, 0.88f, 1f), FontStyles.Bold);
            rightHand = RuntimeUI.Button(canvas, "Droite", new Vector2(300, 206), new Vector2(200, 52), RuntimeUI.Neutral, Color.white, () => SessionSettings.DominantHand = Handedness.Right);
            leftHand = RuntimeUI.Button(canvas, "Gauche", new Vector2(520, 206), new Vector2(200, 52), RuntimeUI.Neutral, Color.white, () => SessionSettings.DominantHand = Handedness.Left);

            RuntimeUI.Text(canvas, "Posture", 22, new Vector2(30, 280), new Vector2(260, 40), new Color(0.8f, 0.88f, 1f), FontStyles.Bold);
            standing = RuntimeUI.Button(canvas, "Debout", new Vector2(300, 272), new Vector2(200, 52), RuntimeUI.Neutral, Color.white, () => SessionSettings.Posture = WorkPosture.Standing);
            seated = RuntimeUI.Button(canvas, "Assis", new Vector2(520, 272), new Vector2(200, 52), RuntimeUI.Neutral, Color.white, () => SessionSettings.Posture = WorkPosture.Seated);

            RuntimeUI.Button(canvas, "Commencer avec la prise en main", new Vector2(30, 354), new Vector2(720, 64), RuntimeUI.CommandYellow, RuntimeUI.TextDark,
                () => SessionFlow.Instance?.StartSession(true), 24);
            skipTutorial = RuntimeUI.Button(canvas, "Commencer sans prise en main (déjà suivie)", new Vector2(30, 428), new Vector2(720, 56), RuntimeUI.Neutral, Color.white,
                () => SessionFlow.Instance?.StartSession(false));
            resume = RuntimeUI.Button(canvas, "Reprendre", new Vector2(30, 494), new Vector2(720, 56), RuntimeUI.InfoBlue, Color.white,
                () => SessionFlow.Instance?.ResumeSession(), out resumeLabel);

            RuntimeUI.Text(canvas, "VR : pointez un bouton avec le rayon et validez avec la gâchette avant. Clavier-souris : clic gauche. Touche B : changer de mode.",
                17, new Vector2(30, 572), new Vector2(720, 40), new Color(0.75f, 0.78f, 0.82f));
        }

        /// <summary>Logo de l'EFREI (projet réalisé dans le cadre du cours), sur une plaque claire en haut à droite.</summary>
        private void AddSchoolLogo()
        {
            var logo = Resources.Load<Sprite>("DeToutesPieces/Logo_EFREI");
            if (logo == null) return;
            var plate = new GameObject("Logo EFREI", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)plate.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -18f);
            rt.sizeDelta = new Vector2(176f, 76f);
            var bg = plate.GetComponent<Image>();
            UITheme.StyleButton(bg);
            bg.color = Color.white;
            bg.raycastTarget = false;
            var img = new GameObject("Image", typeof(RectTransform), typeof(Image));
            var irt = (RectTransform)img.transform;
            irt.SetParent(rt, false);
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(8f, 6f); irt.offsetMax = new Vector2(-8f, -6f);
            var i = img.GetComponent<Image>();
            i.sprite = logo;
            i.preserveAspect = true;
            i.raycastTarget = false;
        }

        private void RefreshOptions()
        {
            if (canvas == null) return;
            Select(rightHand, SessionSettings.DominantHand == Handedness.Right);
            Select(leftHand, SessionSettings.DominantHand == Handedness.Left);
            Select(standing, SessionSettings.Posture == WorkPosture.Standing);
            Select(seated, SessionSettings.Posture == WorkPosture.Seated);
            // Document V1 : la prise en main peut être passée aux sessions suivantes.
            skipTutorial.interactable = SessionSettings.TutorialDone;
            bool hasSave = ProgressSave.HasSave;
            resume.interactable = hasSave;
            resumeLabel.text = hasSave ? $"Reprendre à l'étape {ProgressSave.SavedStep + 1} (dernier jalon enregistré)" : "Aucune session à reprendre";
        }

        private static void Select(Button b, bool selected)
        {
            b.GetComponent<Image>().color = selected ? RuntimeUI.CommandYellow : RuntimeUI.Neutral;
            b.GetComponentInChildren<TMP_Text>().color = selected ? RuntimeUI.TextDark : Color.white;
        }
    }
}
