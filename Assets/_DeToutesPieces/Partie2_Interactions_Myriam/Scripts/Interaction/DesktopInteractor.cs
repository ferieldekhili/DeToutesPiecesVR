using System;
using System.Collections.Generic;
using DeToutesPieces.Parts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeToutesPieces.Interaction
{
    /// <summary>
    /// Saisie à la souris (mode clavier-souris) : équivalent de XRPartGrabBridge pour un ordinateur.
    /// Appelle les mêmes points d'entrée de la logique métier (AssemblyPart.NotifyGrabbed / NotifyReleased) :
    /// silhouettes, trous de vis, clé Allen, annulation et bilan fonctionnent sans changement.
    /// - Clic gauche maintenu : saisir ; relâcher : poser (même validation qu'en VR).
    /// - Molette : rapprocher / éloigner. Ctrl + souris : tourner librement. R / T / Y : quart de tour.
    /// - Aide à la profondeur : quand le pointeur vise une silhouette ou un trou compatible, la pièce est
    ///   amenée à sa profondeur ; l'orientation et la visée restent à la charge de l'utilisateur.
    /// - Clé Allen : viser une vis engagée emboîte la clé ; la molette vers l'avant serre (sens horaire).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DesktopInteractor : MonoBehaviour
    {
        [SerializeField] private float maxReach = 2.5f;
        [SerializeField] private float minHold = 0.2f;
        [SerializeField] private float maxHold = 1.5f;
        [SerializeField] private float depthAssistRadius = 0.09f;
        [SerializeField] private float keyAssistRadius = 0.05f;
        [SerializeField] private float followSharpness = 25f;
        [SerializeField] private float rotateSensitivity = 0.35f;
        [SerializeField] private float twistPerNotch = 18f;

        /// <summary>Distance de l'interface (panneaux) sous le pointeur, fournie par la couche interface ; infini si aucune.</summary>
        public static Func<Vector2, float> UiHitDistance;

        public AssemblyPart Held => held;
        public AllenKey HeldKey => heldKey;

        private Camera cam;
        private DesktopCameraController look;
        private AssemblyPart held;
        private Rigidbody heldRb;
        private AllenKey heldKey;
        private Transform keyTip;
        private Quaternion heldRotation;
        private float holdDistance;
        private PartHighlighter hovered;
        private readonly List<GhostSnapTarget> ghosts = new List<GhostSnapTarget>();
        private float nextGhostScan;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            look = GetComponent<DesktopCameraController>();
        }

        private void OnDisable()
        {
            if (held != null) Release();
            SetHover(null);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || kb == null) return;

            var screen = look != null && look.LookActive
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : mouse.position.ReadValue();
            var ray = cam.ScreenPointToRay(screen);

            // La pièce a pu être prise en charge ailleurs (placement automatique, annulation...).
            if (held != null && held.State != AssemblyPart.PartState.Held) ClearHeld();

            if (held == null)
            {
                Pick(ray, screen, out var part, out float dist);
                SetHover(part != null ? part.GetComponent<PartHighlighter>() : null);
                if (part != null && mouse.leftButton.wasPressedThisFrame) Grab(part, dist);
                return;
            }

            UpdateHeld(ray, mouse, kb);
            if (!mouse.leftButton.isPressed) Release();
        }

        // ---------------- Saisie ----------------

        private bool Pick(Ray ray, Vector2 screen, out AssemblyPart part, out float distance)
        {
            part = null;
            distance = 0f;
            if (!Physics.Raycast(ray, out var hit, maxReach, ~0, QueryTriggerInteraction.Ignore)) return false;
            float ui = UiHitDistance != null ? UiHitDistance(screen) : float.PositiveInfinity;
            if (ui < hit.distance) return false;   // un panneau est devant : le clic lui revient
            var p = hit.collider.GetComponentInParent<AssemblyPart>();
            if (p == null || p.State != AssemblyPart.PartState.Free) return false;
            part = p;
            distance = hit.distance;
            return true;
        }

        private void Grab(AssemblyPart part, float distance)
        {
            SetHover(null);
            held = part;
            heldRb = part.GetComponent<Rigidbody>();
            if (heldRb != null) heldRb.isKinematic = true;
            heldRotation = part.transform.rotation;
            holdDistance = Mathf.Clamp(distance, minHold, maxHold);
            heldKey = part.GetComponent<AllenKey>();
            keyTip = heldKey != null ? FindChild(part.transform, "Embout") : null;
            if (part.TryGetComponent<PartHighlighter>(out var h)) h.SetState(PartHighlighter.HighlightState.Grabbed);
            part.NotifyGrabbed(null);
        }

        private void Release()
        {
            var p = held;
            if (p == null) return;
            if (p.TryGetComponent<PartHighlighter>(out var h)) h.SetState(PartHighlighter.HighlightState.None);
            if (heldRb != null) heldRb.isKinematic = false;   // un placement réussi la refige aussitôt
            ClearHeld();
            p.NotifyReleased();
        }

        private void ClearHeld()
        {
            held = null;
            heldRb = null;
            heldKey = null;
            keyTip = null;
        }

        // ---------------- Pièce tenue ----------------

        private void UpdateHeld(Ray ray, Mouse mouse, Keyboard kb)
        {
            float wheel = mouse.scroll.ReadValue().y;
            int notch = wheel > 0.01f ? 1 : wheel < -0.01f ? -1 : 0;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

            if (heldKey != null && keyTip != null && HandleKey(ray, notch)) return;

            // Rotation : quarts de tour au clavier, rotation libre avec Ctrl + souris.
            if (kb.rKey.wasPressedThisFrame) heldRotation = Quaternion.AngleAxis(90f, Vector3.up) * heldRotation;
            if (kb.tKey.wasPressedThisFrame) heldRotation = Quaternion.AngleAxis(90f, transform.right) * heldRotation;
            if (kb.yKey.wasPressedThisFrame) heldRotation = Quaternion.AngleAxis(90f, transform.forward) * heldRotation;
            if (ctrl)
            {
                var d = mouse.delta.ReadValue() * rotateSensitivity;
                heldRotation = Quaternion.AngleAxis(-d.x, Vector3.up) * Quaternion.AngleAxis(d.y, transform.right) * heldRotation;
                if (notch != 0) heldRotation = Quaternion.AngleAxis(15f * notch, transform.forward) * heldRotation;
            }
            else if (notch != 0)
            {
                holdDistance = Mathf.Clamp(holdDistance + 0.05f * notch, minHold, maxHold);
            }

            var target = ray.GetPoint(holdDistance);
            if (DepthAssist(ray, out float t)) target = ray.GetPoint(t);

            float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            held.transform.SetPositionAndRotation(
                Vector3.Lerp(held.transform.position, target, k),
                Quaternion.Slerp(held.transform.rotation, heldRotation, k));
        }

        /// <summary>Clé Allen : emboîtement assisté puis serrage à la molette. Vrai si la clé est prise en charge.</summary>
        private bool HandleKey(Ray ray, int notch)
        {
            var engaged = heldKey.EngagedSlot;
            if (engaged != null)
            {
                if (DistanceToRay(ray, engaged.HeadPosition, out _) > keyAssistRadius * 2f) return false; // on vise ailleurs : la clé suit
                if (notch != 0) heldKey.SimulateTwist(twistPerNotch * notch);   // molette vers l'avant = sens horaire
                return true;
            }

            ScrewSlot best = null;
            float bestD = keyAssistRadius;
            foreach (var s in ScrewSlot.All)
            {
                if (s == null || s.State != ScrewSlot.SlotState.Engaged || !s.TighteningAllowed) continue;
                float d = DistanceToRay(ray, s.HeadPosition, out float along);
                if (along < 0.05f || along > maxReach || d >= bestD) continue;
                best = s;
                bestD = d;
            }
            if (best == null) return false;

            // Présente l'embout dans l'axe de la vis : la clé s'emboîte d'elle-même à l'image suivante.
            var tr = held.transform;
            tr.rotation = Quaternion.FromToRotation(keyTip.forward, best.transform.forward) * tr.rotation;
            tr.position += best.HeadPosition - keyTip.position;
            heldRotation = tr.rotation;
            return true;
        }

        /// <summary>Profondeur d'une silhouette ou d'un trou compatible situé sous le pointeur.</summary>
        private bool DepthAssist(Ray ray, out float t)
        {
            t = 0f;
            float best = depthAssistRadius;
            bool found = false;

            if (held.definition != null)
            {
                if (Time.unscaledTime >= nextGhostScan)
                {
                    nextGhostScan = Time.unscaledTime + 0.5f;
                    ghosts.Clear();
                    ghosts.AddRange(FindObjectsByType<GhostSnapTarget>(FindObjectsInactive.Exclude));
                }
                foreach (var g in ghosts)
                {
                    if (g == null || !g.IsActiveForStep || g.IsSatisfied || g.ExpectedPart != held.definition) continue;
                    Consider(ray, g.transform.position, ref best, ref t, ref found);
                }
            }
            else if (held.TryGetComponent<FastenerItem>(out _))
            {
                foreach (var s in ScrewSlot.All)
                    if (s != null && s.IsActiveForStep && s.State == ScrewSlot.SlotState.Empty)
                        Consider(ray, s.transform.position, ref best, ref t, ref found);
            }
            return found;
        }

        private void Consider(Ray ray, Vector3 point, ref float best, ref float t, ref bool found)
        {
            float d = DistanceToRay(ray, point, out float along);
            if (along < minHold || along > maxReach || d >= best) return;
            best = d;
            t = along;
            found = true;
        }

        private static float DistanceToRay(Ray ray, Vector3 point, out float along)
        {
            along = Vector3.Dot(point - ray.origin, ray.direction);
            return Vector3.Distance(ray.GetPoint(along), point);
        }

        private void SetHover(PartHighlighter h)
        {
            if (h == hovered) return;
            if (hovered != null && hovered.TryGetComponent<AssemblyPart>(out var old) && old.State == AssemblyPart.PartState.Free)
                hovered.SetState(PartHighlighter.HighlightState.None);
            hovered = h;
            if (hovered != null) hovered.SetState(PartHighlighter.HighlightState.Hover);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
