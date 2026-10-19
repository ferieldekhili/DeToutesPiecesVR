using System;
using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Data;
using DeToutesPieces.Feedback;
using UnityEngine;

namespace DeToutesPieces.Parts
{
    /// <summary>
    /// Pièce physique manipulable du kit. Indépendante du système d'entrée : l'adaptateur XR
    /// (XRPartGrabBridge) ou tout autre contrôleur appelle NotifyGrabbed / NotifyReleased.
    /// Gère aussi le retour automatique au rangement si la pièce tombe hors d'atteinte.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AssemblyPart : MonoBehaviour
    {
        public enum PartState { Free, Held, Placed }

        public PartDefinition definition;

        [Header("Retour au rangement (pièce hors d'atteinte)")]
        [SerializeField] private float returnBelowHeight = 0.12f;
        [SerializeField] private float maxDistanceFromHome = 1.6f;
        [SerializeField] private float returnDelay = 0.8f;

        public PartState State { get; private set; } = PartState.Free;
        public IHapticChannel CurrentHand { get; private set; }
        public GhostSnapTarget PlacedOn { get; private set; }
        public string DisplayName => definition != null ? definition.displayName : name;

        public static readonly List<AssemblyPart> HeldParts = new List<AssemblyPart>();

        public event Action<AssemblyPart> Grabbed;
        public event Action<AssemblyPart> Released;
        public event Action<AssemblyPart> Snapped;
        public event Action<AssemblyPart> Unsnapped;
        public event Action<bool> GrabbableChanged;

        private Rigidbody rb;
        // Pose de rangement exprimée dans le repère du parent d'origine : elle suit
        // automatiquement le plan de travail quand sa hauteur est réglée.
        private Vector3 homeLocalPosition;
        private Quaternion homeLocalRotation;
        private Vector3 HomePosition => homeParent != null ? homeParent.TransformPoint(homeLocalPosition) : homeLocalPosition;
        private Quaternion HomeRotation => homeParent != null ? homeParent.rotation * homeLocalRotation : homeLocalRotation;
        private Transform homeParent;
        private float outOfReachTimer;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            homeParent = transform.parent;
            SetHomePose(transform.position, transform.rotation);
        }

        private void OnDisable() => HeldParts.Remove(this);

        private void Update()
        {
            if (State != PartState.Free) { outOfReachTimer = 0f; return; }

            bool outOfReach = transform.position.y < returnBelowHeight ||
                              Vector3.Distance(transform.position, HomePosition) > maxDistanceFromHome;
            outOfReachTimer = outOfReach ? outOfReachTimer + Time.deltaTime : 0f;

            if (outOfReachTimer > returnDelay)
            {
                ReturnHome();
                AssemblyManager.Instance?.ReportPartReturned();
                FeedbackBus.Emit(FeedbackKind.PartReturned, transform.position, "Pièce remise à sa place.");
            }
        }

        /// <summary>Redéfinit la pose de rangement (coordonnées monde).</summary>
        public void SetHomePose(Vector3 position, Quaternion rotation)
        {
            homeLocalPosition = homeParent != null ? homeParent.InverseTransformPoint(position) : position;
            homeLocalRotation = homeParent != null ? Quaternion.Inverse(homeParent.rotation) * rotation : rotation;
        }

        // ---------- Appels de l'adaptateur d'entrée ----------

        public void NotifyGrabbed(IHapticChannel hand)
        {
            if (State == PartState.Placed) return;
            State = PartState.Held;
            CurrentHand = hand;
            if (!HeldParts.Contains(this)) HeldParts.Add(this);
            FeedbackBus.Emit(FeedbackKind.Grab, transform.position, null, hand);
            Grabbed?.Invoke(this);
        }

public void NotifyReleased()
        {
            if (State != PartState.Held) return;
            var hand = CurrentHand;
            State = PartState.Free;
            HeldParts.Remove(this);
            Released?.Invoke(this);

            // Un seul gestionnaire (le plus proche : silhouette, emplacement de vis...) traite la dépose.
            if (!PartReleaseRouter.Resolve(this, hand))
                FeedbackBus.Emit(FeedbackKind.Release, transform.position, null, hand);

            CurrentHand = null;
        }

        // ---------- Appels des emplacements ----------

public void SnapTo(GhostSnapTarget target)
        {
            AttachTo(target.transform, Vector3.zero, Quaternion.identity);
            PlacedOn = target;
            Snapped?.Invoke(this);
        }

        /// <summary>Fixe la pièce sur un support (pose locale donnée) : cinématique et non saisissable.</summary>
        public void AttachTo(Transform anchor, Vector3 localPosition, Quaternion localRotation)
        {
            State = PartState.Placed;
            HeldParts.Remove(this);
            StopPhysics(true);
            transform.SetParent(anchor, false);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            GrabbableChanged?.Invoke(false);
        }

        public void Unsnap()
        {
            if (State != PartState.Placed) return;
            PlacedOn = null;
            State = PartState.Free;
            transform.SetParent(homeParent, true);
            StopPhysics(false);
            GrabbableChanged?.Invoke(true);
            Unsnapped?.Invoke(this);
        }

        public void ReturnHome()
        {
            if (State == PartState.Placed) Unsnap();
            transform.SetParent(homeParent, true);
            transform.SetPositionAndRotation(HomePosition, HomeRotation);
            StopPhysics(false);
            outOfReachTimer = 0f;
        }

        private void StopPhysics(bool kinematic)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = kinematic;
        }
    }
}
