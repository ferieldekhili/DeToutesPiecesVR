using System;
using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Data;
using DeToutesPieces.Feedback;
using DeToutesPieces.Interaction;
using DeToutesPieces.Parts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Fenêtre d'outils déroulante (amélioration proposée par le professeur).
    /// Se déroule sous le panneau de consigne et liste l'outil, la visserie et les pièces du kit
    /// avec leur état. L'élément utile à l'opération en cours est surligné en jaune.
    /// « Prendre » présente l'élément devant l'utilisateur, à portée de main, jusqu'à ce qu'il le saisisse.
    /// Ouverture : en-tête de la fenêtre, bouton X (main gauche) ou touche O au clavier.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ToolDrawer : MonoBehaviour
    {
        [SerializeField] private float rowHeight = 62f;
        [SerializeField] private float headerHeight = 60f;
        [SerializeField] private float unrollSpeed = 6f;
        [SerializeField] private bool openAtStart;

        [Header("Présentation devant l'utilisateur")]
        [SerializeField] private float presentDistance = 0.42f;
        [SerializeField] private float presentHeightBelowEyes = 0.38f;
        [SerializeField] private float presentSideOffset = 0.12f;

        private class Entry
        {
            public string label;
            public Func<string> status;
            public Func<AssemblyPart> pick;
            public Func<bool> needed;
            public Image background;
            public TMP_Text statusText;
            public Button takeButton;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private RectTransform body;
        private TMP_Text headerLabel;
        private bool open;
        private float unroll;
        private float nextRefresh;
        private InputAction toggleAction;

        private static readonly Color RowColor = new Color(0.12f, 0.15f, 0.2f, 0.95f);
        private static readonly Color RowNeededColor = new Color(0.45f, 0.36f, 0.05f, 0.95f);

        public bool IsOpen => open;

        private void Start()
        {
            BuildEntries();
            BuildUI();
            open = openAtStart;
            unroll = open ? 1f : 0f;
            ApplyUnroll();
            Refresh();
        }

        private void OnEnable()
        {
            toggleAction = new InputAction("Fenêtre d'outils", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            toggleAction.AddBinding("<Keyboard>/o");
            toggleAction.performed += _ => Toggle();
            toggleAction.Enable();
        }

        private void OnDisable()
        {
            toggleAction?.Dispose();
            toggleAction = null;
        }

        public void Toggle()
        {
            open = !open;
            FeedbackBus.Emit(FeedbackKind.Info, null, null);
            Refresh();
        }

        private void Update()
        {
            float target = open ? 1f : 0f;
            if (!Mathf.Approximately(unroll, target))
            {
                unroll = Mathf.MoveTowards(unroll, target, unrollSpeed * Time.unscaledDeltaTime);
                ApplyUnroll();
            }
            if (open && Time.unscaledTime >= nextRefresh) Refresh();
        }

        private void ApplyUnroll()
        {
            if (body == null) return;
            body.localScale = new Vector3(1f, Mathf.SmoothStep(0f, 1f, unroll), 1f);
            body.gameObject.SetActive(unroll > 0.001f);
            if (headerLabel != null) headerLabel.text = open ? "Outils et pièces  (fermer)" : "Outils et pièces  (ouvrir — touche O / bouton X)";
        }

        // ---------------- Contenu ----------------

        private void BuildEntries()
        {
            entries.Clear();
            var parts = FindObjectsByType<AssemblyPart>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

            // Clé Allen
            AssemblyPart key = null;
            foreach (var p in parts) if (p.GetComponent<AllenKey>() != null) { key = p; break; }
            if (key != null)
                entries.Add(new Entry
                {
                    label = "Clé Allen (réf. 100006)",
                    status = () => key.State == AssemblyPart.PartState.Held ? "en main" : key.IsPresented ? "présentée" : "disponible",
                    pick = () => key.State == AssemblyPart.PartState.Free ? key : null,
                    needed = () => CurrentGuidance() is TightenRequirement
                });

            // Visserie : une entrée par référence réellement présente dans le kit.
            var screwsByDef = new Dictionary<FastenerDefinition, List<AssemblyPart>>();
            foreach (var p in parts)
            {
                var f = p.GetComponent<FastenerItem>();
                if (f == null) continue;
                if (!screwsByDef.TryGetValue(f.definition, out var list)) screwsByDef[f.definition] = list = new List<AssemblyPart>();
                list.Add(p);
            }
            foreach (var kv in screwsByDef)
            {
                var def = kv.Key; var list = kv.Value;
                entries.Add(new Entry
                {
                    label = def != null ? $"Vis réf. {def.reference}" : "Vis",
                    status = () => $"{CountFree(list)}/{list.Count} dans le bac",
                    pick = () => FirstFree(list),
                    needed = () => CurrentGuidance() is ScrewSlot s && (s.ExpectedFastener == null || s.ExpectedFastener == def)
                });
            }

            // Pièces du kit, regroupées par définition (dans l'ordre de la notice : pieds, tablette, plateau).
            var byDef = new Dictionary<PartDefinition, List<AssemblyPart>>();
            var order = new List<PartDefinition>();
            foreach (var p in parts)
            {
                if (p.definition == null) continue;
                if (!byDef.TryGetValue(p.definition, out var list)) { byDef[p.definition] = list = new List<AssemblyPart>(); order.Add(p.definition); }
                list.Add(p);
            }
            // Ordre d'apparition dans les étapes de la notice (tablette, pieds, plateau).
            order.Sort((a, b) => FirstUse(a).CompareTo(FirstUse(b)));
            foreach (var def in order)
            {
                var list = byDef[def];
                entries.Add(new Entry
                {
                    label = list.Count > 1 ? $"{def.displayName} ×{list.Count}" : def.displayName,
                    status = () =>
                    {
                        int placed = 0; foreach (var p in list) if (p.State == AssemblyPart.PartState.Placed) placed++;
                        return placed == list.Count ? "posé" + (list.Count > 1 ? "s" : "") : $"{CountFree(list)} disponible(s), {placed} posé(s)";
                    },
                    pick = () => FirstFree(list),
                    needed = () => CurrentGuidance() is GhostSnapTarget g && g.ExpectedPart == def
                });
            }
        }

        private static int FirstUse(PartDefinition def)
        {
            var m = AssemblyManager.Instance;
            if (m == null) return int.MaxValue;
            int i = 0;
            foreach (var step in m.Steps)
                foreach (var r in step.requirements)
                {
                    if (r is GhostSnapTarget g && g.ExpectedPart == def) return i;
                    i++;
                }
            return int.MaxValue;
        }

        private static StepRequirement CurrentGuidance() => AssemblyManager.Instance != null ? AssemblyManager.Instance.CurrentGuidance : null;

        private static int CountFree(List<AssemblyPart> list)
        {
            int n = 0; foreach (var p in list) if (p != null && p.State == AssemblyPart.PartState.Free) n++;
            return n;
        }

        private static AssemblyPart FirstFree(List<AssemblyPart> list)
        {
            foreach (var p in list) if (p != null && p.State == AssemblyPart.PartState.Free && !p.IsPresented) return p;
            return null;
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.25f;
            foreach (var e in entries)
            {
                if (e.statusText == null) continue;
                e.statusText.text = e.status();
                bool needed = e.needed();
                e.background.color = needed ? RowNeededColor : RowColor;
                e.takeButton.interactable = e.pick() != null;
            }
        }

        private void Take(Entry e)
        {
            var part = e.pick();
            if (part == null)
            {
                FeedbackBus.Emit(FeedbackKind.Info, null, $"{e.label} : aucun exemplaire disponible.");
                return;
            }

            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return;
            var flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, flatForward);
            var pos = cam.position + flatForward * presentDistance + right * presentSideOffset + Vector3.down * presentHeightBelowEyes;

            if (part.Present(pos, part.transform.rotation))
            {
                if (part.TryGetComponent(out PartHighlighter hl)) hl.SetState(PartHighlighter.HighlightState.Hint);
                FeedbackBus.Emit(FeedbackKind.Info, pos, $"{e.label} : présenté(e) devant vous, saisissez-le (gâchette latérale).");
            }
            Refresh();
        }

        // ---------------- Interface (construite par code) ----------------

        private void BuildUI()
        {
            var rt = (RectTransform)transform;
            float width = rt.rect.width > 1f ? rt.rect.width : 640f;

            var header = MakeButton(rt, "En-tête", new Vector2(0, 0), new Vector2(width, headerHeight), new Color(1f, 0.8f, 0.1f), out headerLabel, new Color(0.1f, 0.1f, 0.1f), 22);
            header.onClick.AddListener(Toggle);

            var bodyGo = new GameObject("Contenu", typeof(RectTransform), typeof(Image));
            body = (RectTransform)bodyGo.transform;
            body.SetParent(rt, false);
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.sizeDelta = new Vector2(width, entries.Count * rowHeight + 16f);
            body.anchoredPosition = new Vector2(0, -headerHeight - 4f);
            var bodyImg = bodyGo.GetComponent<Image>();
            bodyImg.color = new Color(0.07f, 0.09f, 0.13f, 0.96f);

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var row = new GameObject("Ligne " + e.label, typeof(RectTransform), typeof(Image));
                var rrt = (RectTransform)row.transform;
                rrt.SetParent(body, false);
                rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f);
                rrt.pivot = new Vector2(0.5f, 1f);
                rrt.sizeDelta = new Vector2(width - 16f, rowHeight - 6f);
                rrt.anchoredPosition = new Vector2(0, -8f - i * rowHeight);
                e.background = row.GetComponent<Image>();
                e.background.color = RowColor;

                MakeText(rrt, "Nom", e.label, 20, FontStyles.Bold, Color.white, new Vector2(0f, 0f), new Vector2(0.42f, 1f));
                e.statusText = MakeText(rrt, "État", "", 18, FontStyles.Normal, new Color(0.8f, 0.88f, 1f), new Vector2(0.42f, 0f), new Vector2(0.78f, 1f));

                var take = MakeButton(rrt, "Prendre", Vector2.zero, new Vector2(120f, rowHeight - 18f), new Color(0.2f, 0.45f, 0.85f), out _, Color.white, 18);
                var trt = (RectTransform)take.transform;
                trt.anchorMin = trt.anchorMax = new Vector2(1f, 0.5f);
                trt.pivot = new Vector2(1f, 0.5f);
                trt.anchoredPosition = new Vector2(-8f, 0f);
                take.GetComponentInChildren<TMP_Text>().text = "Prendre";
                var captured = e;
                take.onClick.AddListener(() => Take(captured));
                e.takeButton = take;
            }
        }

        private static Button MakeButton(RectTransform parent, string name, Vector2 anchoredPos, Vector2 size, Color bg, out TMP_Text label, Color fg, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            go.GetComponent<Image>().color = bg;
            var b = go.GetComponent<Button>();
            var c = b.colors; c.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f); b.colors = c;
            label = MakeText(rt, "Libellé", name, fontSize, FontStyles.Bold, fg, Vector2.zero, Vector2.one);
            label.alignment = TextAlignmentOptions.Center;
            return b;
        }

        private static TMP_Text MakeText(RectTransform parent, string name, string text, float size, FontStyles style, Color color, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(12f, 4f); rt.offsetMax = new Vector2(-8f, -4f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }
    }
}
