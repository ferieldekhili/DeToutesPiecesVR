using DeToutesPieces.Core;
using DeToutesPieces.Feedback;
using DeToutesPieces.Parts;
using UnityEngine;

namespace DeToutesPieces.Interaction
{
    /// <summary>
    /// Clé Allen (réf. 100006) — ACV Agir : « introduire et tourner la clé ».
    /// 1. Engagement assisté : quand l'embout tenu arrive près d'une vis engagée, à peu près dans l'axe,
    ///    la clé s'emboîte visuellement dans la tête de vis.
    /// 2. Serrage abrégé : la rotation du poignet autour de l'axe de la vis fait progresser le serrage.
    ///    Plusieurs rotations courtes remplacent les tours réels ; relâcher la clé conserve l'avancement.
    /// 3. Sens : horaire vu depuis la tête de vis. Le sens inverse est signalé, sans faire régresser la vis.
    /// Le serrage n'est possible que pendant l'étape 6 : avant, la notice demande de laisser les vis desserrées.
    /// </summary>
    [RequireComponent(typeof(AssemblyPart))]
    public class AllenKey : MonoBehaviour
    {
        [Header("Géométrie")]
        [Tooltip("Extrémité de la clé ; son axe +Z sort de l'embout.")]
        [SerializeField] private Transform tip;
        [Tooltip("Racine des rendus de la clé (masqués pendant l'engagement et remplacés par la copie emboîtée).")]
        [SerializeField] private Transform visualRoot;

        [Header("Engagement (à ajuster sur casque)")]
        [SerializeField] private float engageDistance = 0.035f;
        [SerializeField] private float engageAngle = 40f;
        [Tooltip("Distance embout / tête au-delà de laquelle la clé se déboite. Large : la torsion du poignet déplace l'embout.")]
        [SerializeField] private float disengageDistance = 0.15f;
        [Tooltip("Écart angulaire au-delà duquel la clé sort de la tête de vis (permet de passer à la vis voisine).")]
        [SerializeField] private float disengageAngle = 65f;

        [Header("Serrage")]
        [Tooltip("Rotation cumulée nécessaire pour serrer une vis (degrés).")]
        [SerializeField] private float degreesToTighten = 270f;
        [SerializeField] private float maxDegreesPerFrame = 45f;
        [SerializeField] private float tickEveryDegrees = 30f;
        [SerializeField] private float wrongWayWarningDegrees = 25f;

        private AssemblyPart part;
        private ScrewSlot engaged;
        private Transform engagedPivot;
        private Vector3 lastRef;
        private Vector3 referenceLocalAxis;
        private float tickAccumulator;
        private float wrongWayAccumulator;
        private bool wrongWayWarned;
        private float nextInfoTime;
        private Renderer[] ownRenderers;

        public ScrewSlot EngagedSlot => engaged;

        private void Awake()
        {
            part = GetComponent<AssemblyPart>();
            if (visualRoot == null) visualRoot = transform;
            ownRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        }

        private void Update()
        {
            if (part.State != AssemblyPart.PartState.Held)
            {
                if (engaged != null) Disengage(false);
                return;
            }

            if (engaged == null) TryEngage();
            else UpdateTightening();
        }

        // ---------------- Engagement ----------------

        private void TryEngage()
        {
            ScrewSlot best = null;
            float bestDist = float.MaxValue;
            bool notYetAllowed = false;

            foreach (var s in ScrewSlot.All)
            {
                if (s == null || s.State != ScrewSlot.SlotState.Engaged) continue;
                float d = Vector3.Distance(tip.position, s.HeadPosition);
                if (d > engageDistance) continue;
                if (!s.TighteningAllowed) { notYetAllowed = true; continue; }
                if (Vector3.Angle(tip.forward, s.transform.forward) > engageAngle)
                {
                    InfoOnce("Alignez la clé avec la vis : l'embout doit entrer dans l'axe de la tête.");
                    continue;
                }
                // Parmi les vis proches et dans l'axe, la mieux alignée puis la plus proche l'emporte.
                float score = d + Vector3.Angle(tip.forward, s.transform.forward) * 0.001f;
                if (score < bestDist) { best = s; bestDist = score; }
            }

            if (best != null) { Engage(best); return; }
            if (notYetAllowed)
                InfoOnce("Pas encore : la notice demande de laisser les vis desserrées jusqu'au serrage final (étape 6).");
        }

        private void Engage(ScrewSlot slot)
        {
            engaged = slot;
            var axis = Axis();

            // Copie de la clé emboîtée dans la tête de vis : seule la rotation autour de l'axe est suivie.
            engagedPivot = new GameObject("Clé emboîtée").transform;
            engagedPivot.SetParent(slot.transform, true);
            var copy = Instantiate(visualRoot.gameObject, engagedPivot, true);
            foreach (var c in copy.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var r in copy.GetComponentsInChildren<Renderer>()) r.enabled = true;
            // Aligne l'embout de la copie sur la tête de vis, dans l'axe d'insertion.
            var align = Quaternion.FromToRotation(tip.forward, slot.transform.forward);
            engagedPivot.position = slot.HeadPosition;
            copy.transform.SetPositionAndRotation(
                slot.HeadPosition + align * (visualRoot.position - tip.position),
                align * visualRoot.rotation);

            foreach (var r in ownRenderers) if (r) r.enabled = false;

            // Référence de torsion : l'axe local de la clé le plus perpendiculaire à l'axe de la vis.
            referenceLocalAxis = Mathf.Abs(Vector3.Dot(transform.right, axis)) < Mathf.Abs(Vector3.Dot(transform.up, axis))
                ? Vector3.right : Vector3.up;
            lastRef = ReferenceVector(axis);
            tickAccumulator = 0f;
            wrongWayAccumulator = 0f;
            wrongWayWarned = false;

            FeedbackBus.Emit(FeedbackKind.ScrewEngaged, slot.HeadPosition,
                "Clé engagée. Tournez le poignet dans le sens des aiguilles d'une montre (flèche jaune).", part.CurrentHand);
        }

        private void Disengage(bool completed)
        {
            foreach (var r in ownRenderers) if (r) r.enabled = true;
            if (engagedPivot != null) Destroy(engagedPivot.gameObject);
            engagedPivot = null;
            engaged = null;
        }

        // ---------------- Serrage ----------------

        // Axe orienté vers l'utilisateur (sortie du trou) : une rotation positive autour de cet axe
        // apparaît dans le sens horaire pour l'utilisateur qui regarde la tête de vis.
        private Vector3 Axis() => -engaged.transform.forward;

        private Vector3 ReferenceVector(Vector3 axis) =>
            Vector3.ProjectOnPlane(transform.TransformDirection(referenceLocalAxis), axis).normalized;

        private void UpdateTightening()
        {
            if (Vector3.Distance(tip.position, engaged.HeadPosition) > disengageDistance ||
                Vector3.Angle(tip.forward, engaged.transform.forward) > disengageAngle)
            {
                Disengage(false);
                return;
            }

            var axis = Axis();
            var current = ReferenceVector(axis);
            if (current.sqrMagnitude < 0.5f) return;
            float delta = Mathf.Clamp(Vector3.SignedAngle(lastRef, current, axis), -maxDegreesPerFrame, maxDegreesPerFrame);
            lastRef = current;

            if (delta > 0.2f)
            {
                engaged.AddTightenProgress(delta / degreesToTighten);
                if (engagedPivot != null) engagedPivot.Rotate(axis, delta, Space.World);
                wrongWayAccumulator = 0f;

                tickAccumulator += delta;
                if (tickAccumulator >= tickEveryDegrees)
                {
                    tickAccumulator = 0f;
                    FeedbackBus.Emit(FeedbackKind.ScrewProgress, engaged.HeadPosition, null, part.CurrentHand);
                }

                if (engaged.State == ScrewSlot.SlotState.Tightened)
                {
                    var pos = engaged.HeadPosition;
                    Disengage(true);
                    FeedbackBus.Emit(FeedbackKind.ScrewTightened, pos, "Vis serrée.", part.CurrentHand);
                }
            }
            else if (delta < -0.2f)
            {
                wrongWayAccumulator += -delta;
                if (!wrongWayWarned && wrongWayAccumulator > wrongWayWarningDegrees)
                {
                    wrongWayWarned = true;
                    AssemblyManager.Instance?.ReportFasteningError();
                    FeedbackBus.Emit(FeedbackKind.WrongAction, engaged.HeadPosition,
                        "Sens inverse : pour serrer, tournez dans le sens des aiguilles d'une montre (flèche jaune).", part.CurrentHand);
                }
            }
        }

        private void InfoOnce(string message)
        {
            if (Time.time < nextInfoTime) return;
            nextInfoTime = Time.time + 4f;
            FeedbackBus.Emit(FeedbackKind.Info, tip.position, message, part.CurrentHand);
        }

        /// <summary>Pour les tests automatisés : simule une rotation du poignet de « degrees » (positif = sens horaire).</summary>
        public void SimulateTwist(float degrees)
        {
            if (engaged == null) return;
            transform.Rotate(Axis(), degrees, Space.World);
            UpdateTightening();
        }
    }
}
