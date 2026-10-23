using UnityEngine;
using UnityEngine.InputSystem;

namespace DeToutesPieces.Interaction
{
    /// <summary>
    /// Caméra du mode clavier-souris : regard au clic droit maintenu, déplacement au clavier.
    /// Les touches sont lues par position physique (ZQSD sur AZERTY = WASD sur QWERTY), comme le simulateur XR.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DesktopCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.3f;
        [SerializeField] private float fastMultiplier = 2.2f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [Tooltip("Limites de déplacement de la tête (pièce de l'atelier).")]
        [SerializeField] private Vector3 boundsMin = new Vector3(-3.1f, 0.6f, -3.1f);
        [SerializeField] private Vector3 boundsMax = new Vector3(3.1f, 2.2f, 1.9f);

        private float yaw, pitch;

        /// <summary>Vrai pendant que le clic droit est maintenu (curseur capturé).</summary>
        public bool LookActive { get; private set; }

        public void SetPose(Vector3 position, float yawDegrees, float pitchDegrees)
        {
            yaw = yawDegrees;
            pitch = Mathf.Clamp(pitchDegrees, -80f, 80f);
            transform.SetPositionAndRotation(Clamp(position), Quaternion.Euler(pitch, yaw, 0f));
        }

        public void SetEyeHeight(float y)
        {
            var p = transform.position;
            p.y = y;
            transform.position = Clamp(p);
        }

        private void OnDisable() => SetLook(false);

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            SetLook(mouse.rightButton.isPressed);
            if (LookActive)
            {
                var d = mouse.delta.ReadValue();
                yaw += d.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * lookSensitivity, -80f, 80f);
            }

            // Ctrl est réservé à la rotation de la pièce tenue : pas de déplacement pendant ce temps.
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            var move = Vector3.zero;
            if (!ctrl)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.z -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.eKey.isPressed || kb.pageUpKey.isPressed) move.y += 1f;
                if (kb.qKey.isPressed || kb.pageDownKey.isPressed) move.y -= 1f;
            }
            float speed = moveSpeed * (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? fastMultiplier : 1f);
            var flat = Quaternion.Euler(0f, yaw, 0f);
            var p = transform.position + (flat * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y) * speed * Time.unscaledDeltaTime;
            transform.SetPositionAndRotation(Clamp(p), Quaternion.Euler(pitch, yaw, 0f));
        }

        private void SetLook(bool active)
        {
            if (active == LookActive) return;
            LookActive = active;
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
        }

        private Vector3 Clamp(Vector3 p) => new Vector3(
            Mathf.Clamp(p.x, boundsMin.x, boundsMax.x),
            Mathf.Clamp(p.y, boundsMin.y, boundsMax.y),
            Mathf.Clamp(p.z, boundsMin.z, boundsMax.z));
    }
}
