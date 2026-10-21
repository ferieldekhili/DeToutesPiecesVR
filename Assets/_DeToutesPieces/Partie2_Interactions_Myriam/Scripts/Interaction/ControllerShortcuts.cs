using DeToutesPieces.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeToutesPieces.Interaction
{
    /// <summary>
    /// Raccourcis des manettes définis dans le document (I² cognitives) :
    /// A = indice / répéter, B = annuler, Menu (main gauche) = pause.
    /// Raccourcis clavier équivalents pour les tests sur ordinateur : F (indice), U (annuler), Entrée (étape suivante), Échap (pause).
    /// Les boutons A/B suivent la main dominante choisie.
    /// </summary>
    public class ControllerShortcuts : MonoBehaviour
    {
        public enum Hand { Right, Left }

        [SerializeField] private Hand dominantHand = Hand.Right;
        [SerializeField] private bool keyboardFallback = true;

        private InputAction hintAction;
        private InputAction undoAction;
        private InputAction menuAction;
        private InputAction nextAction;

        public Hand DominantHand
        {
            get => dominantHand;
            set { dominantHand = value; Rebuild(); }
        }

        private void OnEnable()
        {
            SessionSettings.Changed += ApplySettings;
            ApplySettings();
        }

        private void OnDisable()
        {
            SessionSettings.Changed -= ApplySettings;
            DisposeActions();
        }

        // Main dominante choisie à l'accueil ou dans le menu de réglages.
        private void ApplySettings()
        {
            dominantHand = SessionSettings.DominantHand == Handedness.Left ? Hand.Left : Hand.Right;
            Rebuild();
        }

        private void Rebuild()
        {
            DisposeActions();
            string hand = dominantHand == Hand.Right ? "{RightHand}" : "{LeftHand}";

            hintAction = new InputAction("Hint", InputActionType.Button, $"<XRController>{hand}/primaryButton");
            undoAction = new InputAction("Undo", InputActionType.Button, $"<XRController>{hand}/secondaryButton");
            menuAction = new InputAction("Menu", InputActionType.Button, "<XRController>{LeftHand}/menuButton");
            menuAction.AddBinding("<XRController>{RightHand}/menuButton");
            nextAction = new InputAction("Next", InputActionType.Button);

            if (keyboardFallback)
            {
                // Touches choisies pour ne pas entrer en conflit avec le XR Interaction Simulator
                // (H = tête, N = pincée de main, WASD/QE, G, T, 1-9, [, ], Tab, R, Z/X/C/V... déjà utilisées).
                hintAction.AddBinding("<Keyboard>/f");
                undoAction.AddBinding("<Keyboard>/u");
                menuAction.AddBinding("<Keyboard>/escape");
                nextAction.AddBinding("<Keyboard>/enter");
            }

            hintAction.performed += _ => AssemblyManager.Instance?.RequestHint();
            undoAction.performed += _ => AssemblyManager.Instance?.Undo();
            menuAction.performed += _ =>
            {
                var m = AssemblyManager.Instance;
                if (m != null) m.SetPaused(!m.IsPaused);
            };
            nextAction.performed += _ =>
            {
                // Hors montage (prise en main, inventaire), « suivant » termine ou passe la phase.
                var flow = SessionFlow.Instance;
                if (flow != null && (flow.Phase == SessionPhase.PriseEnMain || flow.Phase == SessionPhase.Inventaire))
                    flow.CompletePhase(flow.Phase);
                else
                    AssemblyManager.Instance?.ConfirmNextStep();
            };

            hintAction.Enable(); undoAction.Enable(); menuAction.Enable(); nextAction.Enable();
        }

        private void DisposeActions()
        {
            hintAction?.Dispose(); undoAction?.Dispose(); menuAction?.Dispose(); nextAction?.Dispose();
            hintAction = undoAction = menuAction = nextAction = null;
        }
    }
}
