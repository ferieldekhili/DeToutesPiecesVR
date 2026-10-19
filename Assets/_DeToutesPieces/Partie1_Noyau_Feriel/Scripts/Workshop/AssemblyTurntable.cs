using DeToutesPieces.Core;
using UnityEngine;

namespace DeToutesPieces.Workshop
{
    /// <summary>
    /// Support tournant de l'assemblage : au début de chaque étape, tourne l'ouvrage vers l'orientation
    /// prévue (AssemblyStep.turntableYaw). Transcription des « retournements » de la notice :
    /// l'utilisateur garde les fixations à traiter face à lui sans déplacement ni manipulation lourde.
    /// </summary>
    public class AssemblyTurntable : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 120f; // degrés / s

        private float targetYaw;
        private float baseYaw;

        private void Awake()
        {
            baseYaw = transform.localEulerAngles.y;
            targetYaw = baseYaw;
        }

        private void OnEnable()
        {
            if (AssemblyManager.Instance != null) AssemblyManager.Instance.StepStarted += OnStepStarted;
        }

private void Start()
        {
            var m = AssemblyManager.Instance;
            if (m == null) return;
            m.StepStarted -= OnStepStarted;
            m.StepStarted += OnStepStarted;
            m.GuidanceTargetChanged -= OnGuidance;
            m.GuidanceTargetChanged += OnGuidance;
            if (m.CurrentStep != null) OnStepStarted(m.CurrentStepIndex, m.CurrentStep);
        }

        // Étape « suit le guidage » : présente face à l'utilisateur (côté -Z) la prochaine opération.
        private void OnGuidance(StepRequirement req)
        {
            var m = AssemblyManager.Instance;
            if (m == null || m.CurrentStep == null || !m.CurrentStep.turntableFollowsGuidance || req == null) return;
            var local = transform.InverseTransformPoint(req.GuidanceTarget.position);
            targetYaw = baseYaw + (local.z > 0.01f ? 180f : 0f);
        }

private void OnDisable()
        {
            if (AssemblyManager.Instance == null) return;
            AssemblyManager.Instance.StepStarted -= OnStepStarted;
            AssemblyManager.Instance.GuidanceTargetChanged -= OnGuidance;
        }

        private void OnStepStarted(int index, AssemblyStep step) => targetYaw = baseYaw + step.turntableYaw;

        private void Update()
        {
            var e = transform.localEulerAngles;
            float y = Mathf.MoveTowardsAngle(e.y, targetYaw, rotationSpeed * Time.deltaTime);
            if (!Mathf.Approximately(y, e.y)) transform.localEulerAngles = new Vector3(e.x, y, e.z);
        }
    }
}
