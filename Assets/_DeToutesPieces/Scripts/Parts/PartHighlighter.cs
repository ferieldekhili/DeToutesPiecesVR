using DeToutesPieces.Feedback;
using UnityEngine;

namespace DeToutesPieces.Parts
{
    /// <summary>
    /// Teinte les rendus d'un objet selon son état d'interaction (code couleur du document) :
    /// blanc = pointé, bleu = saisi, vert = correct, rouge = erreur, jaune = guidage / indice.
    /// Utilise des MaterialPropertyBlock : aucun matériau n'est dupliqué.
    /// </summary>
    public class PartHighlighter : MonoBehaviour
    {
        public enum HighlightState { None, Hover, Grabbed, Valid, Invalid, Hint }

        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.45f;
        [SerializeField] private float hintBlinkSpeed = 4f;

        private Renderer[] renderers;
        private Color[] baseColors;
        private MaterialPropertyBlock block;
        private HighlightState state;
        private HighlightState transientState;
        private float transientUntil;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public HighlightState State => state;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            CacheRenderers();
        }

        public void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            }
        }

        public void SetState(HighlightState s)
        {
            state = s;
            Apply();
        }

        /// <summary>Affiche brièvement un état (ex. rouge après une erreur) puis revient à l'état courant.</summary>
        public void Flash(HighlightState s, float duration = 0.6f)
        {
            transientState = s;
            transientUntil = Time.time + duration;
        }

        private void Update()
        {
            if (transientUntil > 0f && Time.time > transientUntil) { transientUntil = 0f; Apply(); }
            if (transientUntil > 0f || state == HighlightState.Hint) Apply();
        }

        private void Apply()
        {
            if (renderers == null) return;
            var s = transientUntil > Time.time ? transientState : state;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(block);
                if (s == HighlightState.None)
                {
                    block.Clear();
                }
                else
                {
                    float k = tintStrength;
                    if (s == HighlightState.Hint) k *= 0.5f + 0.5f * Mathf.Sin(Time.time * hintBlinkSpeed);
                    block.SetColor(BaseColorId, Color.Lerp(baseColors[i], ColorOf(s), k));
                }
                renderers[i].SetPropertyBlock(block);
            }
        }

        private static Color ColorOf(HighlightState s)
        {
            switch (s)
            {
                case HighlightState.Hover: return FeedbackService.HoverWhite;
                case HighlightState.Grabbed: return FeedbackService.InfoBlue;
                case HighlightState.Valid: return FeedbackService.SuccessGreen;
                case HighlightState.Invalid: return FeedbackService.ErrorRed;
                case HighlightState.Hint: return FeedbackService.CommandYellow;
                default: return Color.white;
            }
        }
    }
}
