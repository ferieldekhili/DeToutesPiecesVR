using DeToutesPieces.Core;
using UnityEngine;

namespace DeToutesPieces.Workshop
{
    /// <summary>
    /// Poste de montage réglable en hauteur (posture assise ou debout, document section 5).
    /// Déplace verticalement le plan de travail, le support du meuble et les zones de rangement,
    /// (les poses de rangement des pièces étant relatives à ce plan, elles suivent sans code supplémentaire).
    /// </summary>
    public class AdjustableWorkstation : MonoBehaviour
    {
        public enum Posture { Standing, Seated }

        [Tooltip("Racine de tout ce qui monte et descend avec le plan de travail.")]
        [SerializeField] private Transform adjustableRoot;
        [SerializeField] private float standingOffset = 0f;
        [SerializeField] private float seatedOffset = -0.18f;
        [SerializeField] private float minOffset = -0.3f;
        [SerializeField] private float maxOffset = 0.15f;

        public float CurrentOffset { get; private set; }

        private Vector3 basePosition;

        private void Awake()
        {
            if (adjustableRoot == null) adjustableRoot = transform;
            basePosition = adjustableRoot.localPosition;
        }

        private void OnEnable()
        {
            SessionSettings.Changed += ApplySettings;
        }

        private void Start() => ApplySettings();

        private void OnDisable() => SessionSettings.Changed -= ApplySettings;

        // Posture choisie à l'accueil ou dans le menu de réglages.
        private void ApplySettings() =>
            SetPosture(SessionSettings.Posture == WorkPosture.Seated ? Posture.Seated : Posture.Standing);

        public void SetPosture(Posture p) => SetOffset(p == Posture.Standing ? standingOffset : seatedOffset);

        public void Nudge(float delta) => SetOffset(CurrentOffset + delta);

        public void SetOffset(float offset)
        {
            offset = Mathf.Clamp(offset, minOffset, maxOffset);
            float delta = offset - CurrentOffset;
            if (Mathf.Approximately(delta, 0f)) return;
            CurrentOffset = offset;
            adjustableRoot.localPosition = basePosition + Vector3.up * offset;
            // Les pièces rangées suivent automatiquement : leur pose de rangement est relative au plan.
        }
    }
}
