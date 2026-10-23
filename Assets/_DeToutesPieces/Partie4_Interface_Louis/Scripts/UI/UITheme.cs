using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Identité graphique commune des panneaux VR (Partie 4) : fonds ardoise à coins arrondis, cadre fin,
    /// liseré jaune, commandes jaunes, informations bleues. Les sprites (générés par l'outil d'interface,
    /// dossier Resources/DeToutesPieces) sont chargés à l'exécution ; sans eux, les couleurs s'appliquent seules.
    /// </summary>
    public static class UITheme
    {
        public static readonly Color Panel = new Color(0.105f, 0.122f, 0.157f, 0.97f);
        public static readonly Color PanelRaised = new Color(0.150f, 0.172f, 0.215f, 1f);
        public static readonly Color Frame = new Color(0.40f, 0.44f, 0.52f, 0.9f);
        public static readonly Color Yellow = new Color(1f, 0.80f, 0.10f);
        public static readonly Color Blue = new Color(0.02f, 0.38f, 0.72f);
        public static readonly Color Neutral = new Color(0.23f, 0.26f, 0.32f);
        public static readonly Color TextMain = new Color(0.96f, 0.95f, 0.92f);
        public static readonly Color TextSoft = new Color(0.72f, 0.76f, 0.82f);
        public static readonly Color Label = new Color(0.62f, 0.77f, 0.96f);
        public static readonly Color TextOnYellow = new Color(0.10f, 0.10f, 0.11f);
        public static readonly Color Paper = new Color(0.955f, 0.945f, 0.915f, 0.98f);

        private static Sprite rounded, outline, button;
        private static bool loaded;

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            rounded = Resources.Load<Sprite>("DeToutesPieces/UI_Arrondi");
            outline = Resources.Load<Sprite>("DeToutesPieces/UI_Contour");
            button = Resources.Load<Sprite>("DeToutesPieces/UI_Bouton");
        }

        public static Sprite Rounded { get { Load(); return rounded; } }
        public static Sprite Outline { get { Load(); return outline; } }
        public static Sprite ButtonSprite { get { Load(); return button; } }

        private static void Apply(Image img, Sprite s)
        {
            if (img == null || s == null) return;
            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>Fond de panneau : ardoise à coins arrondis.</summary>
        public static void StyleBackground(Image img, Color? color = null)
        {
            Apply(img, Rounded);
            if (img != null) img.color = color ?? Panel;
        }

        public static void StyleButton(Image img) => Apply(img, ButtonSprite);

        /// <summary>Ajoute le cadre fin et le liseré jaune d'en-tête (une seule fois).</summary>
        public static void Decorate(RectTransform panel, Color? frameColor = null, bool accent = true)
        {
            if (panel == null) return;
            if (panel.Find("Cadre") == null && Outline != null)
            {
                var f = new GameObject("Cadre", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)f.transform;
                rt.SetParent(panel, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var img = f.GetComponent<Image>();
                Apply(img, Outline);
                img.color = frameColor ?? Frame;
                img.raycastTarget = false;
                rt.SetAsFirstSibling();
            }
            if (accent && panel.Find("Liseré") == null)
            {
                var a = new GameObject("Liseré", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)a.transform;
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(30f, -3f);
                rt.sizeDelta = new Vector2(72f, 4f);
                var img = a.GetComponent<Image>();
                img.color = Yellow;
                img.raycastTarget = false;
            }
        }

        /// <summary>Applique la police et l'espacement des titres.</summary>
        public static void StyleTitle(TMP_Text t)
        {
            if (t == null) return;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 1.5f;
        }
    }
}
