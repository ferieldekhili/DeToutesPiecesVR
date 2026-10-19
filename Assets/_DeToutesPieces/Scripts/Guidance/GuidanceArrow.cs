using DeToutesPieces.Core;
using UnityEngine;

namespace DeToutesPieces.Guidance
{
    /// <summary>
    /// Flèche de guidage (ACV Observer) : flotte au-dessus de la prochaine opération à réaliser
    /// (silhouette à remplir, trou à visser...) et la désigne en pointant vers elle.
    /// Suit automatiquement la cible fournie par l'AssemblyManager.
    /// </summary>
    public class GuidanceArrow : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [Tooltip("Hauteur au-dessus de la cible.")]
        [SerializeField] private float hoverHeight = 0.14f;
        [SerializeField] private float bobAmplitude = 0.025f;
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float followSpeed = 8f;
        [SerializeField] private bool visibleByDefault = true;

        private Transform target;
        private Vector3 offset;
        private Vector3 direction = Vector3.down;
        private StepRequirement currentRequirement;
        private bool userVisible;

        public bool UserVisible
        {
            get => userVisible;
            set { userVisible = value; Refresh(); }
        }

        private void Awake()
        {
            userVisible = visibleByDefault;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        }

        private void OnEnable()
        {
            var m = AssemblyManager.Instance;
            if (m != null)
            {
                m.GuidanceTargetChanged += OnTargetChanged;
                OnTargetChanged(m.CurrentGuidance);
            }
            else Refresh();
        }

        private void Start()
        {
            // Si le manager s'est initialisé après nous.
            if (AssemblyManager.Instance != null)
            {
                AssemblyManager.Instance.GuidanceTargetChanged -= OnTargetChanged;
                AssemblyManager.Instance.GuidanceTargetChanged += OnTargetChanged;
                OnTargetChanged(AssemblyManager.Instance.CurrentGuidance);
            }
        }

        private void OnDisable()
        {
            if (AssemblyManager.Instance != null) AssemblyManager.Instance.GuidanceTargetChanged -= OnTargetChanged;
        }

private void OnTargetChanged(StepRequirement req)
        {
            currentRequirement = req;
            direction = req != null ? req.GuidanceDirection.normalized : Vector3.down;
            if (direction.sqrMagnitude < 0.5f) direction = Vector3.down;
            SetTarget(req != null ? req.GuidanceTarget : null);
        }

        public void SetTarget(Transform t, Vector3 localOffset = default)
        {
            bool jump = target == null && t != null;
            target = t;
            offset = localOffset;
            if (jump) transform.position = GoalPosition();
            Refresh();
        }

        private void Refresh()
        {
            if (visual != null) visual.gameObject.SetActive(userVisible && target != null);
        }

private Vector3 GoalPosition() => target.position + offset - direction * hoverHeight;

private void LateUpdate()
        {
            if (target == null) return;
            if (currentRequirement != null) direction = currentRequirement.GuidanceDirection.normalized; // suit le support tournant
            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, GoalPosition(), k);
            // La pointe (axe -Y local du modèle) vise la cible selon la direction demandée.
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.FromToRotation(Vector3.down, direction), k);
            if (visual != null)
            {
                visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
                visual.Rotate(Vector3.up, 90f * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>Maillage de flèche (tige + pointe conique) orientée vers -Y, pointe à l'origine.</summary>
        public static Mesh BuildArrowMesh(float length = 0.09f, float headLength = 0.04f, float headRadius = 0.022f, float shaftRadius = 0.008f, int segments = 20)
        {
            var mesh = new Mesh { name = "GuidanceArrow" };
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            void Ring(float y, float r)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
            }
            void Side(int ringA, int ringB)
            {
                for (int i = 0; i < segments; i++)
                {
                    int n = (i + 1) % segments;
                    tris.Add(ringA + i); tris.Add(ringB + i); tris.Add(ringA + n);
                    tris.Add(ringA + n); tris.Add(ringB + i); tris.Add(ringB + n);
                }
            }
            void Cap(int ring, Vector3 center, bool up)
            {
                int c = verts.Count; verts.Add(center);
                for (int i = 0; i < segments; i++)
                {
                    int n = (i + 1) % segments;
                    if (up) { tris.Add(c); tris.Add(ring + n); tris.Add(ring + i); }
                    else { tris.Add(c); tris.Add(ring + i); tris.Add(ring + n); }
                }
            }

            // Pointe : sommet en (0,0,0), base en y = headLength.
            int tip = verts.Count; verts.Add(Vector3.zero);
            int headBase = verts.Count; Ring(headLength, headRadius);
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                tris.Add(tip); tris.Add(headBase + n); tris.Add(headBase + i);
            }
            Cap(headBase, new Vector3(0, headLength, 0), true);

            // Tige.
            int shaftBottom = verts.Count; Ring(headLength, shaftRadius);
            int shaftTop = verts.Count; Ring(headLength + length, shaftRadius);
            Side(shaftTop, shaftBottom);
            Cap(shaftTop, new Vector3(0, headLength + length, 0), true);

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
