using DeToutesPieces.Core;
using DeToutesPieces.Feedback;
using UnityEngine;

namespace DeToutesPieces.Guidance
{
    /// <summary>
    /// Contrôle visuel (ACV Observer) : satisfait lorsque l'utilisateur regarde la zone indiquée
    /// pendant une durée minimale. Utilisé pour la vérification d'équerre des pieds (notice p. 12).
    /// </summary>
    public class GazeCheckRequirement : StepRequirement
    {
        [SerializeField] private string label = "Observer le meuble : les pieds doivent être d'équerre";
        [SerializeField] private string completionMessage = "Contrôle visuel effectué : les pieds sont droits et d'équerre.";
        [SerializeField] private float requiredSeconds = 2f;
        [SerializeField] private float maxAngle = 25f;
        [SerializeField] private float maxDistance = 2.5f;

        private float timer;
        private bool done;
        private Transform cam;

        public override bool IsSatisfied => done;
        public override string Label => label;
        public float Progress => Mathf.Clamp01(timer / requiredSeconds);

        public override void SetActiveForStep(bool active)
        {
            base.SetActiveForStep(active);
            timer = 0f;
        }

        private void Update()
        {
            if (!IsActiveForStep || done) return;

            // N'est évalué que lorsque c'est la dernière opération restante de l'étape.
            var m = AssemblyManager.Instance;
            if (m != null && m.CurrentGuidance != this) { timer = 0f; return; }

            // Caméra active (VR ou clavier-souris) : réévaluée si le mode a changé.
            if ((cam == null || !cam.gameObject.activeInHierarchy) && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;

            var toTarget = transform.position - cam.position;
            bool looking = toTarget.magnitude <= maxDistance && Vector3.Angle(cam.forward, toTarget) <= maxAngle;
            timer = looking ? timer + Time.deltaTime : Mathf.Max(0f, timer - Time.deltaTime);

            if (timer >= requiredSeconds)
            {
                done = true;
                FeedbackBus.Emit(FeedbackKind.StepReady, transform.position, completionMessage);
                RaiseStateChanged();
            }
        }

        public override bool TryAutoComplete()
        {
            ForceComplete();
            return true;
        }

        /// <summary>Pour les tests automatisés.</summary>
        public void ForceComplete()
        {
            if (done) return;
            done = true;
            RaiseStateChanged();
        }
    }
}
