using DeToutesPieces.Feedback;
using DeToutesPieces.Parts;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DeToutesPieces.Interaction
{
    /// <summary>
    /// Adaptateur entre XR Interaction Toolkit et la logique métier (AssemblyPart, PartHighlighter).
    /// Saisie : maintien de la gâchette latérale (grip). Grande pièce : prise à deux mains autorisée.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable), typeof(AssemblyPart))]
    public class XRPartGrabBridge : MonoBehaviour
    {
        [Tooltip("Autorise la prise à deux mains (pièces volumineuses : plateau, tablette, cadres).")]
        [SerializeField] private bool allowTwoHands = true;

        private XRGrabInteractable grab;
        private AssemblyPart part;
        private PartHighlighter highlighter;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            part = GetComponent<AssemblyPart>();
            TryGetComponent(out highlighter);

            grab.selectMode = allowTwoHands ? InteractableSelectMode.Multiple : InteractableSelectMode.Single;
            grab.throwOnDetach = false; // une pièce lâchée est posée, pas lancée
        }

        private void OnEnable()
        {
            grab.hoverEntered.AddListener(OnHoverEntered);
            grab.hoverExited.AddListener(OnHoverExited);
            grab.selectEntered.AddListener(OnSelectEntered);
            grab.selectExited.AddListener(OnSelectExited);
            part.GrabbableChanged += OnGrabbableChanged;
        }

        private void OnDisable()
        {
            grab.hoverEntered.RemoveListener(OnHoverEntered);
            grab.hoverExited.RemoveListener(OnHoverExited);
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
            part.GrabbableChanged -= OnGrabbableChanged;
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (part.State == AssemblyPart.PartState.Free && highlighter != null)
                highlighter.SetState(PartHighlighter.HighlightState.Hover);
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (!grab.isHovered && part.State == AssemblyPart.PartState.Free && highlighter != null)
                highlighter.SetState(PartHighlighter.HighlightState.None);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            // Deuxième main sur une pièce déjà tenue : rien de plus à signaler.
            if (grab.interactorsSelecting.Count > 1) return;
            if (highlighter != null) highlighter.SetState(PartHighlighter.HighlightState.Grabbed);
            part.NotifyGrabbed(XRHapticChannel.From(args.interactorObject));
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            // La pièce n'est relâchée que lorsque plus aucune main ne la tient.
            if (grab.interactorsSelecting.Count > 0) return;
            if (highlighter != null) highlighter.SetState(PartHighlighter.HighlightState.None);
            part.NotifyReleased();
        }

        private void OnGrabbableChanged(bool canGrab)
        {
            // Pièce placée : stabilisée jusqu'à annulation (document V1, « maintenir les pièces »).
            grab.enabled = canGrab;
            if (!canGrab && highlighter != null) highlighter.SetState(PartHighlighter.HighlightState.None);
        }
    }

    /// <summary>Canal haptique d'une manette XR (HapticImpulsePlayer des Starter Assets).</summary>
    public class XRHapticChannel : IHapticChannel
    {
        private readonly HapticImpulsePlayer player;

        private XRHapticChannel(HapticImpulsePlayer p) { player = p; }

        public static IHapticChannel From(IXRInteractor interactor)
        {
            if (interactor == null) return null;
            var p = interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
            return p != null ? new XRHapticChannel(p) : null;
        }

        public void Pulse(float amplitude, float duration)
        {
            if (player != null) player.SendHapticImpulse(amplitude, duration);
        }
    }
}
