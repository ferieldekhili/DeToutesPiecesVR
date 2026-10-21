using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Fabrique d'interface en espace monde, utilisée par les écrans construits par code
    /// (accueil, pause / réglages, bilan). Code couleur du document : commandes jaunes, informations bleues.
    /// </summary>
    public static class RuntimeUI
    {
        public static readonly Color Background = new Color(0.07f, 0.09f, 0.13f, 0.97f);
        public static readonly Color CommandYellow = new Color(1f, 0.8f, 0.1f);
        public static readonly Color InfoBlue = new Color(0.2f, 0.45f, 0.85f);
        public static readonly Color Neutral = new Color(0.35f, 0.38f, 0.45f);
        public static readonly Color TextDark = new Color(0.1f, 0.1f, 0.1f);

        public static RectTransform CreateWorldCanvas(Transform parent, string name, Vector2 sizePx)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 10;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx;
            rt.localScale = Vector3.one * 0.001f;
            var bg = go.AddComponent<Image>();
            bg.color = Background;
            return rt;
        }

        /// <summary>Texte positionné par son coin haut-gauche (en pixels depuis le haut du parent).</summary>
        public static TMP_Text Text(RectTransform parent, string text, float size, Vector2 topLeft, Vector2 sizePx,
            Color color, FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject("Texte", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rt.sizeDelta = sizePx;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(RectTransform parent, string label, Vector2 topLeft, Vector2 sizePx, Color bg, Color fg,
            UnityEngine.Events.UnityAction onClick, out TMP_Text labelText, float fontSize = 22f)
        {
            var go = new GameObject("Bouton " + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rt.sizeDelta = sizePx;
            go.GetComponent<Image>().color = bg;
            var b = go.GetComponent<Button>();
            var c = b.colors; c.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f); c.highlightedColor = new Color(1f, 1f, 1f, 0.85f); b.colors = c;
            if (onClick != null) b.onClick.AddListener(onClick);
            labelText = Text(rt, label, fontSize, new Vector2(6f, 4f), sizePx - new Vector2(12f, 8f), fg, FontStyles.Bold, TextAlignmentOptions.Center);
            return b;
        }

        public static Button Button(RectTransform parent, string label, Vector2 topLeft, Vector2 sizePx, Color bg, Color fg,
            UnityEngine.Events.UnityAction onClick, float fontSize = 22f) =>
            Button(parent, label, topLeft, sizePx, bg, fg, onClick, out _, fontSize);

        /// <summary>Place un écran face à l'utilisateur, à hauteur d'yeux, au-dessus du poste.</summary>
        public static void PlaceInFrontOfUser(Transform screen, float distance, float heightOffset)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            screen.position = cam.position + fwd * distance + Vector3.up * heightOffset;
            screen.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }
}
