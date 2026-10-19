using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Parts;
using UnityEngine;

namespace DeToutesPieces.Guidance
{
    /// <summary>
    /// Guide du sens de rotation de la clé Allen : flèche circulaire animée (sens des aiguilles d'une montre,
    /// vu par l'utilisateur) + jauge de progression du serrage, affichées sur la tête de la vis à serrer.
    /// Repère local : +Z pointe vers l'utilisateur (sortie du trou).
    /// </summary>
    public class RotationGuide : MonoBehaviour
    {
        [SerializeField] private MeshRenderer arrowRenderer;
        [SerializeField] private MeshFilter progressFilter;
        [SerializeField] private MeshRenderer progressRenderer;
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private float progressRadius = 0.017f;

        private ScrewSlot target;
        private float shownProgress = -1f;
        private Mesh progressMesh;

        public ScrewSlot Target => target;

        private void Awake()
        {
            progressMesh = new Mesh { name = "ProgressArc" };
            if (progressFilter != null) progressFilter.sharedMesh = progressMesh;
            Show(null);
        }

        private void OnEnable()
        {
            if (AssemblyManager.Instance != null) AssemblyManager.Instance.GuidanceTargetChanged += OnGuidance;
        }

        private void Start()
        {
            var m = AssemblyManager.Instance;
            if (m == null) return;
            m.GuidanceTargetChanged -= OnGuidance;
            m.GuidanceTargetChanged += OnGuidance;
            OnGuidance(m.CurrentGuidance);
        }

        private void OnDisable()
        {
            if (AssemblyManager.Instance != null) AssemblyManager.Instance.GuidanceTargetChanged -= OnGuidance;
        }

        // Affiché automatiquement sur la vis à serrer désignée par la flèche de guidage.
        private void OnGuidance(StepRequirement req) => Show((req as TightenRequirement)?.Slot);

        public void Show(ScrewSlot slot)
        {
            target = slot;
            shownProgress = -1f;
            if (arrowRenderer) arrowRenderer.enabled = slot != null;
            if (progressRenderer) progressRenderer.enabled = slot != null;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.SetPositionAndRotation(target.HeadPosition - target.transform.forward * 0.002f,
                Quaternion.LookRotation(-target.transform.forward, Vector3.up));

            // Rotation positive autour de +Z local (vers l'utilisateur) = sens horaire vu par l'utilisateur.
            if (arrowRenderer) arrowRenderer.transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);

            float p = target.TightenProgress;
            if (!Mathf.Approximately(p, shownProgress))
            {
                shownProgress = p;
                BuildArc(progressMesh, progressRadius, 0.003f, 360f * Mathf.Max(0.001f, p), 48, false);
            }
        }

        /// <summary>
        /// Arc plat dans le plan XY local, parcouru dans le sens horaire vu depuis +Z.
        /// Avec arrowHead, une pointe de flèche termine l'arc.
        /// </summary>
        public static Mesh BuildArc(Mesh mesh, float radius, float width, float degrees, int segments, bool arrowHead)
        {
            mesh = mesh != null ? mesh : new Mesh();
            mesh.Clear();
            var v = new List<Vector3>();
            var t = new List<int>();
            int n = Mathf.Max(2, Mathf.CeilToInt(segments * degrees / 360f));
            // Vu depuis +Z (axe X apparaissant à gauche), un angle croissant de X vers Y tourne en sens horaire.
            float start = 90f;
            for (int i = 0; i <= n; i++)
            {
                float a = (start + degrees * i / n) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v.Add(dir * (radius - width / 2f));
                v.Add(dir * (radius + width / 2f));
                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    t.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                    t.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 }); // double face
                }
            }
            if (arrowHead)
            {
                float end = (start + degrees) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(end), Mathf.Sin(end), 0f);
                var tangent = new Vector3(-Mathf.Sin(end), Mathf.Cos(end), 0f); // direction d'un angle croissant
                int c = v.Count;
                v.Add(radial * (radius - width * 1.8f));
                v.Add(radial * (radius + width * 1.8f));
                v.Add(radial * radius + tangent * width * 3f);
                t.AddRange(new[] { c, c + 1, c + 2, c, c + 2, c + 1 });
            }
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
