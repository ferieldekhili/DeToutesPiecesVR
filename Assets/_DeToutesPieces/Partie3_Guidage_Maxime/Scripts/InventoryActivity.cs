using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Interaction;
using DeToutesPieces.Parts;
using TMPro;
using UnityEngine;

namespace DeToutesPieces.Guidance
{
    /// <summary>
    /// Inventaire (document V1 : « observer et identifier les éléments ; afficher leur nom et le nombre de vis ») :
    /// une étiquette contextuelle flotte au-dessus de chaque type d'élément du kit. L'utilisateur saisit au moins
    /// une fois chaque type pour l'identifier ; l'étiquette passe alors au vert. Les pièces sont ensuite rangées.
    /// </summary>
    public class InventoryActivity : MonoBehaviour
    {
        [SerializeField] private float labelHeight = 0.13f;
        [SerializeField] private Color pendingColor = new Color(0.12f, 0.23f, 0.54f);
        [SerializeField] private Color seenColor = new Color(0.1f, 0.55f, 0.2f);

        private class Group
        {
            public string name;
            public string description;
            public readonly List<AssemblyPart> parts = new List<AssemblyPart>();
            public bool seen;
            public TextMeshPro label;
        }

        private readonly List<Group> groups = new List<Group>();
        private readonly Dictionary<AssemblyPart, Group> byPart = new Dictionary<AssemblyPart, Group>();
        private bool running;
        private SessionFlow flow;
        private Transform cam;

        private void Start()
        {
            flow = SessionFlow.Instance;
            if (flow == null) return;
            flow.PhaseChanged += OnPhase;
            OnPhase(flow.Phase);
        }

        private void OnDestroy()
        {
            if (flow != null) flow.PhaseChanged -= OnPhase;
        }

        private void OnPhase(SessionPhase phase)
        {
            if (phase == SessionPhase.Inventaire && !running) Begin();
            else if (phase != SessionPhase.Inventaire && running) End();
        }

        // ---------------- Déroulement ----------------

        private void Begin()
        {
            running = true;
            BuildGroups();
            foreach (var g in groups)
            {
                g.label = CreateLabel(g);
                foreach (var p in g.parts) p.Grabbed += OnGrabbed;
            }
            AssemblyManager.Instance?.PublishGuidance(null);
            Publish();
        }

        private void BuildGroups()
        {
            groups.Clear();
            byPart.Clear();
            var all = FindObjectsByType<AssemblyPart>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

            var defGroups = new Dictionary<Object, Group>();
            Group keyGroup = null;
            foreach (var p in all)
            {
                if (PracticeItem.IsPractice(p)) continue;
                Group g;
                if (p.GetComponent<AllenKey>() != null)
                {
                    if (keyGroup == null) keyGroup = new Group { name = "Clé Allen", description = "réf. 100006 — serrage final" };
                    g = keyGroup;
                }
                else if (p.TryGetComponent(out FastenerItem f))
                {
                    if (!defGroups.TryGetValue(f.definition, out g))
                    {
                        string reference = f.definition != null ? f.definition.reference : "?";
                        defGroups[f.definition] = g = new Group { name = "Vis", description = $"réf. {reference} — une seule référence" };
                    }
                }
                else if (p.definition != null)
                {
                    if (!defGroups.TryGetValue(p.definition, out g))
                        defGroups[p.definition] = g = new Group { name = p.definition.displayName, description = "" };
                }
                else continue;
                g.parts.Add(p);
                byPart[p] = g;
            }
            foreach (var g in defGroups.Values) groups.Add(g);
            if (keyGroup != null) groups.Add(keyGroup);
            foreach (var g in groups) if (g.parts.Count > 1) g.name += $" ×{g.parts.Count}";
        }

        private void OnGrabbed(AssemblyPart p)
        {
            if (!running || !byPart.TryGetValue(p, out var g) || g.seen) return;
            g.seen = true;
            if (g.label != null) { g.label.color = seenColor; g.label.text = $"<b>{g.name}</b> (identifié)\n<size=70%>{g.description}</size>"; }
            Feedback.FeedbackBus.Emit(Feedback.FeedbackKind.PlacementPreviewValid, p.transform.position, $"Identifié : {g.name}.");
            Publish();
        }

        private void Publish()
        {
            int seen = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var g in groups)
            {
                if (g.seen) seen++;
                sb.Append(g.seen ? "<color=#3CD95A><b>[OK]</b></color> " : "<color=#FFCC1A><b>[  ]</b></color> ");
                sb.AppendLine(g.name);
            }
            bool done = seen == groups.Count;
            flow.SetPhaseInfo($"Inventaire du kit — {seen}/{groups.Count}",
                done ? "Tous les éléments sont identifiés. Ils vont être rangés à leur place : appuyez sur « Continuer » pour commencer le montage."
                     : "Saisissez au moins une fois chaque type d'élément pour l'identifier ; son nom flotte au-dessus. " +
                       "Observez les trous des pieds, les bords repliés de la tablette et du plateau, la tête des vis.",
                sb.ToString(), done);
        }

        private void End()
        {
            running = false;
            foreach (var g in groups)
            {
                foreach (var p in g.parts)
                {
                    if (p == null) continue;
                    p.Grabbed -= OnGrabbed;
                    // Remise en ordre du poste avant le montage (pièces non tenues).
                    if (p.State == AssemblyPart.PartState.Free) p.ReturnHome();
                }
                if (g.label != null) Destroy(g.label.gameObject);
            }
            groups.Clear();
            byPart.Clear();
        }

        // ---------------- Étiquettes ----------------

        private TextMeshPro CreateLabel(Group g)
        {
            var center = Vector3.zero;
            float top = float.MinValue;
            foreach (var p in g.parts)
            {
                center += p.transform.position;
                top = Mathf.Max(top, p.transform.position.y);
            }
            center /= Mathf.Max(1, g.parts.Count);
            var go = new GameObject("Étiquette inventaire " + g.name);
            go.transform.position = new Vector3(center.x, top + labelHeight, center.z);
            var t = go.AddComponent<TextMeshPro>();
            t.text = $"<b>{g.name}</b>\n<size=70%>{(string.IsNullOrEmpty(g.description) ? "à identifier" : g.description)}</size>";
            t.fontSize = 0.42f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = pendingColor;
            t.rectTransform.sizeDelta = new Vector2(0.5f, 0.12f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private void LateUpdate()
        {
            if (!running) return;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;
            foreach (var g in groups)
            {
                if (g.label == null) continue;
                var dir = g.label.transform.position - cam.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) g.label.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        /// <summary>Pour les tests automatisés : marque tous les éléments comme identifiés.</summary>
        public void MarkAllSeen()
        {
            foreach (var g in groups) if (!g.seen && g.parts.Count > 0) OnGrabbed(g.parts[0]);
        }
    }
}
