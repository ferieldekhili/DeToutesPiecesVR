using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Data;
using DeToutesPieces.Feedback;
using UnityEngine;

namespace DeToutesPieces.Parts
{
    /// <summary>
    /// Trou de fixation d'un pied (ACV Agir : présenter et engager une vis).
    /// Le transform est placé à la surface du pied ; son axe +Z (forward) est la direction d'insertion.
    /// - Actif seulement si son étape est en cours ET si la pièce porteuse est déjà positionnée.
    /// - Repère cible coloré (couleur de la référence attendue) tant que le trou est vide.
    /// - Engagement assisté : une vis de la bonne référence lâchée à proximité s'aligne dans le trou, non serrée.
    /// Le serrage à la clé Allen (étape 6) est traité par un composant séparé (phase 2).
    /// </summary>
    public class ScrewSlot : StepRequirement, IPartReleaseHandler
    {
        public enum SlotState { Empty, Engaged, Tightened }

        [SerializeField] private FastenerDefinition expectedFastener;
        [Tooltip("Silhouette de la pièce porteuse : le trou n'est utilisable qu'une fois celle-ci positionnée.")]
        [SerializeField] private GhostSnapTarget host;
        [SerializeField] private string locationLabel = "trou";
        [Tooltip("Rayon (m) dans lequel une vis lâchée est engagée dans ce trou.")]
        [SerializeField] private float engageRadius = 0.045f;
        [SerializeField] private GameObject targetMarker;

        public SlotState State { get; private set; } = SlotState.Empty;
        public FastenerItem Screw { get; private set; }
        public FastenerDefinition ExpectedFastener => expectedFastener;

        /// <summary>Tous les trous de la scène (utilisé par la clé Allen pour trouver la vis visée).</summary>
        public static readonly List<ScrewSlot> All = new List<ScrewSlot>();

        /// <summary>Avancement du serrage simulé (0 = engagée, 1 = serrée).</summary>
        public float TightenProgress { get; private set; }

        /// <summary>Vrai seulement pendant l'étape de serrage final (notice p. 12).</summary>
        public bool TighteningAllowed { get; private set; }

        public string LocationLabel => locationLabel;

        /// <summary>Position du dessus de la tête de vis (point d'engagement de l'embout).</summary>
        public Vector3 HeadPosition =>
            transform.position - transform.forward * ((Screw != null ? Screw.looseProtrusion * (1f - TightenProgress) : 0f) + 0.004f);

        public void SetTighteningAllowed(bool allowed) => TighteningAllowed = allowed;

        /// <summary>Fait progresser le serrage (appelé par la clé Allen). La vis s'enfonce progressivement.</summary>
        public void AddTightenProgress(float delta)
        {
            if (State != SlotState.Engaged || Screw == null) return;
            TightenProgress = Mathf.Clamp01(TightenProgress + delta);
            Screw.transform.localPosition = Vector3.back * Screw.looseProtrusion * (1f - TightenProgress);
            if (TightenProgress >= 1f) MarkTightened();
        }

        public override bool IsSatisfied => State != SlotState.Empty;
        public override string Label => $"Engager une vis ({locationLabel})";
        public override Vector3 GuidanceDirection => transform.forward;

        private bool HostReady => host == null || host.IsSatisfied;

        private void Awake() => RefreshMarker();

        private void OnEnable()
        {
            if (host != null) host.StateChanged += OnHostChanged;
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            if (host != null) host.StateChanged -= OnHostChanged;
            All.Remove(this);
            PartReleaseRouter.Unregister(this);
        }

        private void OnHostChanged(StepRequirement _)
        {
            // Pièce porteuse retirée (annulation) : la vis éventuelle revient au bac.
            if (!HostReady && Screw != null) RemoveScrew();
            RefreshMarker();
        }

        public override void SetActiveForStep(bool active)
        {
            base.SetActiveForStep(active);
            if (active) PartReleaseRouter.Register(this); else PartReleaseRouter.Unregister(this);
            RefreshMarker();
        }

        private void RefreshMarker()
        {
            if (targetMarker != null) targetMarker.SetActive(IsActiveForStep && HostReady && State == SlotState.Empty);
        }

        // ---------- Dépose d'une vis ----------

        public bool CanConsider(AssemblyPart part, out float distance)
        {
            distance = Vector3.Distance(part.transform.position, transform.position);
            return IsActiveForStep && HostReady && State == SlotState.Empty &&
                   part.TryGetComponent<FastenerItem>(out _) && distance <= engageRadius;
        }

        public bool HandleRelease(AssemblyPart part, IHapticChannel hand)
        {
            var item = part.GetComponent<FastenerItem>();
            if (expectedFastener != null && item.definition != expectedFastener)
            {
                AssemblyManager.Instance?.ReportFasteningError();
                FeedbackService.Emit(FeedbackKind.WrongAction, transform.position,
                    $"Mauvaise vis : ce trou attend la référence {expectedFastener.reference}.", hand);
                return true;
            }

            FeedbackService.Emit(FeedbackKind.ScrewEngaged, transform.position,
                "Vis engagée. Laissez-la desserrée : le serrage final se fait à l'étape 6.", hand);
            Engage(item);
            AssemblyManager.Instance?.RegisterAction(new EngageUndo(this));
            return true;
        }

        private void Engage(FastenerItem item)
        {
            Screw = item;
            State = SlotState.Engaged;
            // Pivot de la vis = dessous de la tête ; axe de la vis = +Y local -> aligné sur -forward (tête vers l'extérieur).
            var rot = Quaternion.FromToRotation(Vector3.up, Vector3.back);
            item.Part.AttachTo(transform, Vector3.back * item.looseProtrusion, rot);
            RefreshMarker();
            RaiseStateChanged();
        }

        /// <summary>Retire la vis et la renvoie au bac (annulation).</summary>
        public void RemoveScrew()
        {
            if (Screw == null) return;
            var s = Screw;
            Screw = null;
            State = SlotState.Empty;
            TightenProgress = 0f;
            s.Part.ReturnHome();
            RefreshMarker();
            RaiseStateChanged();
        }

        /// <summary>Appelé par l'outil de serrage (phase 2).</summary>
        public void MarkTightened()
        {
            if (State != SlotState.Engaged) return;
            State = SlotState.Tightened;
            TightenProgress = 1f;
            if (Screw != null) Screw.transform.localPosition = Vector3.zero;
            RaiseStateChanged();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position - transform.forward * 0.03f, transform.position);
            Gizmos.DrawWireSphere(transform.position, 0.004f);
        }

        private class EngageUndo : IUndoableAction
        {
            private readonly ScrewSlot slot;
            public EngageUndo(ScrewSlot s) { slot = s; }
            public string Label => $"vis ({slot.locationLabel})";
            public void Undo() => slot.RemoveScrew();
        }
    }
}
