using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Data;
using DeToutesPieces.Feedback;
using UnityEngine;

namespace DeToutesPieces.Parts
{
    /// <summary>
    /// Silhouette fantôme indiquant la pose attendue d'une pièce (aide au positionnement).
    /// Le transform de cet objet EST la pose cible du pivot de la pièce.
    /// - Pendant le transport : la silhouette passe au vert (pose acceptable) ou au rouge (mauvaise pièce / orientation).
    /// - À la dépose : alignement assisté (snap) uniquement si pièce, orientation et position sont compatibles.
    /// </summary>
    public class GhostSnapTarget : StepRequirement, IPartReleaseHandler
    {
        public enum Evaluation { OutOfRange, WrongPart, WrongOrientation, TooFar, Valid }

        [Header("Pièce attendue")]
        [SerializeField] private PartDefinition expectedPart;

        [Header("Tolérances (à ajuster sur casque)")]
        [Tooltip("Distance maximale (m) entre le pivot de la pièce et la cible pour accepter l'alignement.")]
        [SerializeField] private float positionTolerance = 0.06f;
        [Tooltip("Écart angulaire maximal (degrés) pour accepter l'alignement.")]
        [SerializeField] private float angleTolerance = 20f;
        [Tooltip("Rayon (m) dans lequel la silhouette réagit à une pièce tenue.")]
        [SerializeField] private float detectionRadius = 0.25f;
        [Tooltip("La pièce est symétrique : une rotation de 180° autour de cet axe local est aussi acceptée.")]
        [SerializeField] private bool symmetric180;
        [SerializeField] private Vector3 symmetryAxis = Vector3.up;

        [Header("Visuel")]
        [SerializeField] private GameObject ghostVisual;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color validColor = new Color(0.2f, 0.85f, 0.35f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(0.95f, 0.22f, 0.2f, 0.45f);

        // Les déposes sont réparties par PartReleaseRouter.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private AssemblyPart placedPart;
        private Evaluation lastPreview = Evaluation.OutOfRange;
        private Renderer[] ghostRenderers;
        private MaterialPropertyBlock block;

        public PartDefinition ExpectedPart => expectedPart;
        public AssemblyPart PlacedPart => placedPart;
        public override bool IsSatisfied => placedPart != null;
        public override string Label => expectedPart != null ? $"Positionner « {expectedPart.displayName} »" : "Positionner une pièce";

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (ghostVisual != null) ghostRenderers = ghostVisual.GetComponentsInChildren<Renderer>(true);
            RefreshVisual();
        }

        private void OnDisable() => PartReleaseRouter.Unregister(this);

        public override void SetActiveForStep(bool active)
        {
            base.SetActiveForStep(active);
            if (active) PartReleaseRouter.Register(this);
            else PartReleaseRouter.Unregister(this);
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (ghostVisual != null) ghostVisual.SetActive(IsActiveForStep && !IsSatisfied);
            SetGhostColor(idleColor);
            lastPreview = Evaluation.OutOfRange;
        }

        // ---------- Prévisualisation pendant le transport ----------

        private void Update()
        {
            if (!IsActiveForStep || IsSatisfied || ghostVisual == null) return;

            AssemblyPart nearest = null;
            float best = detectionRadius;
            foreach (var p in AssemblyPart.HeldParts)
            {
                if (p.definition == null) continue; // visserie : gérée par les emplacements de vis
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d < best) { best = d; nearest = p; }
            }

            var eval = nearest != null ? Evaluate(nearest) : Evaluation.OutOfRange;
            if (eval == lastPreview) return;

            switch (eval)
            {
                case Evaluation.Valid:
                    SetGhostColor(validColor);
                    FeedbackBus.Emit(FeedbackKind.PlacementPreviewValid, transform.position, null, nearest.CurrentHand);
                    break;
                case Evaluation.WrongPart:
                case Evaluation.WrongOrientation:
                    SetGhostColor(invalidColor);
                    break;
                default:
                    SetGhostColor(idleColor);
                    break;
            }
            lastPreview = eval;
        }

        public Evaluation Evaluate(AssemblyPart part)
        {
            float dist = Vector3.Distance(part.transform.position, transform.position);
            if (dist > detectionRadius) return Evaluation.OutOfRange;
            if (part.definition != expectedPart) return Evaluation.WrongPart;
            if (AngleTo(part.transform.rotation) > angleTolerance) return Evaluation.WrongOrientation;
            if (dist > positionTolerance) return Evaluation.TooFar;
            return Evaluation.Valid;
        }

        private float AngleTo(Quaternion rotation)
        {
            float a = Quaternion.Angle(rotation, transform.rotation);
            if (symmetric180)
            {
                var alt = transform.rotation * Quaternion.AngleAxis(180f, symmetryAxis);
                a = Mathf.Min(a, Quaternion.Angle(rotation, alt));
            }
            return a;
        }

        // ---------- Dépose ----------

        /// <summary>Choisit l'emplacement actif le plus proche et lui confie la dépose. Retourne vrai si un emplacement a réagi.</summary>
public bool CanConsider(AssemblyPart part, out float distance)
        {
            distance = Vector3.Distance(part.transform.position, transform.position);
            return IsActiveForStep && !IsSatisfied && part.definition != null && distance <= detectionRadius;
        }

        public bool HandleRelease(AssemblyPart part, IHapticChannel hand)
        {
            part.TryGetComponent(out PartHighlighter highlighter);
            switch (Evaluate(part))
            {
                case Evaluation.WrongPart:
                    AssemblyManager.Instance?.ReportPlacementError();
                    if (highlighter != null) highlighter.Flash(PartHighlighter.HighlightState.Invalid);
                    FeedbackBus.Emit(FeedbackKind.PlacementInvalid, part.transform.position,
                        $"Ce n'est pas la bonne pièce : cet emplacement attend « {expectedPart.displayName} ».", hand);
                    return true;

                case Evaluation.WrongOrientation:
                    AssemblyManager.Instance?.ReportPlacementError();
                    if (highlighter != null) highlighter.Flash(PartHighlighter.HighlightState.Invalid);
                    FeedbackBus.Emit(FeedbackKind.PlacementInvalid, part.transform.position,
                        "Bonne pièce, mais mauvaise orientation : tournez-la pour qu'elle corresponde à la silhouette.", hand);
                    return true;

                case Evaluation.TooFar:
                    FeedbackBus.Emit(FeedbackKind.Info, part.transform.position,
                        "Rapprochez la pièce de la silhouette pour l'aligner.", hand);
                    return true;

                case Evaluation.Valid:
                    // Retour d'abord, puis changement d'état : le message « étape terminée » éventuel s'affiche en dernier.
                    FeedbackBus.Emit(FeedbackKind.PlacementValid, transform.position,
                        $"Pièce en place : « {part.DisplayName} ». Elle reste maintenue en attendant les fixations.", hand);
                    if (highlighter != null) highlighter.Flash(PartHighlighter.HighlightState.Valid);
                    Place(part);
                    AssemblyManager.Instance?.RegisterAction(new PlacementUndo(this));
                    return true;
            }
            return false;
        }

        private void Place(AssemblyPart part)
        {
            placedPart = part;
            part.SnapTo(this);
            RefreshVisual();
            RaiseStateChanged();
        }

        /// <summary>Retire la pièce placée et la renvoie à son rangement (annulation).</summary>
        public void RemovePart()
        {
            if (placedPart == null) return;
            var p = placedPart;
            placedPart = null;
            p.ReturnHome();
            RefreshVisual();
            RaiseStateChanged();
        }

        private void SetGhostColor(Color c)
        {
            if (ghostRenderers == null) return;
            foreach (var r in ghostRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, positionTolerance);
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }

        private class PlacementUndo : IUndoableAction
        {
            private readonly GhostSnapTarget target;
            private readonly string label;
            public PlacementUndo(GhostSnapTarget t) { target = t; label = $"placement de « {t.expectedPart?.displayName} »"; }
            public string Label => label;
            public void Undo() => target.RemovePart();
        }
    }
}
