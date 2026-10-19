using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Parts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeToutesPieces.Guidance
{
    /// <summary>
    /// Notice virtuelle synchronisée avec les étapes (amélioration proposée par le professeur).
    /// Affiche une maquette 3D miniature du résultat attendu à la fin de l'étape :
    /// pièces déjà posées en gris, pièces de l'étape en jaune, vis à engager / serrer marquées,
    /// avec le renvoi vers la page de la notice papier IKEA. Les illustrations IKEA ne sont pas reproduites.
    /// L'utilisateur peut feuilleter les étapes (aperçu) puis revenir à l'étape en cours.
    /// </summary>
    public class VirtualNotice : MonoBehaviour
    {
        [Header("Maquette")]
        [SerializeField] private Transform modelRoot;
        [SerializeField] private float modelScale = 0.4f;
        [SerializeField] private float spinSpeed = 20f;
        [SerializeField] private Material doneMaterial;
        [SerializeField] private Material currentMaterial;
        [SerializeField] private Material screwMaterial;

        [Header("Textes et boutons")]
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text details;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button currentButton;

        private class Item
        {
            public int step;        // étape où l'élément apparaît
            public bool isScrew;
            public int tightenStep = -1;
            public GameObject go;
            public Renderer[] renderers;
        }

        private readonly List<Item> items = new List<Item>();
        private Transform container;
        private int shownStep = -1;
        private AssemblyManager manager;

        public int ShownStep => shownStep;

        private void Start()
        {
            manager = AssemblyManager.Instance;
            if (manager == null) return;
            BuildModel();
            manager.StepStarted += (i, s) => Show(i);
            manager.AssemblyCompleted += () => Show(manager.Steps.Count - 1);
            if (previousButton) previousButton.onClick.AddListener(() => Show(shownStep - 1));
            if (nextButton) nextButton.onClick.AddListener(() => Show(shownStep + 1));
            if (currentButton) currentButton.onClick.AddListener(() => Show(manager.CurrentStepIndex));
            Show(Mathf.Max(0, manager.CurrentStepIndex));
        }

        private void Update()
        {
            if (container != null) container.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        }

        // ---------------- Construction de la maquette ----------------

        private void BuildModel()
        {
            if (modelRoot == null) return;
            container = new GameObject("Maquette (échelle réduite)").transform;
            container.SetParent(modelRoot, false);
            container.localScale = Vector3.one * modelScale;

            for (int s = 0; s < manager.Steps.Count; s++)
            {
                foreach (var req in manager.Steps[s].requirements)
                {
                    if (req is GhostSnapTarget ghost) AddGhostCopy(ghost, s);
                    else if (req is ScrewSlot slot) AddScrewMarker(slot, s);
                    else if (req is TightenRequirement t && t.Slot != null)
                        foreach (var it in items) if (it.isScrew && it.go.name == t.Slot.name) it.tightenStep = s;
                }
            }
        }

        private Transform AssemblyRoot(Transform t)
        {
            // Racine commune des silhouettes : le parent direct des cibles de placement.
            return t.parent;
        }

        private void AddGhostCopy(GhostSnapTarget ghost, int step)
        {
            var visual = ghost.transform.Find("Silhouette");
            if (visual == null) return;
            var root = AssemblyRoot(ghost.transform);
            var holder = new GameObject(ghost.name).transform;
            holder.SetParent(container, false);
            holder.localPosition = root.InverseTransformPoint(ghost.transform.position) - new Vector3(0f, 0.24f, 0f);
            holder.localRotation = Quaternion.Inverse(root.rotation) * ghost.transform.rotation;
            var copy = Instantiate(visual.gameObject, holder, false);
            copy.SetActive(true);
            foreach (var c in copy.GetComponentsInChildren<Collider>()) Destroy(c);
            items.Add(new Item { step = step, go = holder.gameObject, renderers = copy.GetComponentsInChildren<Renderer>(true) });
        }

        private void AddScrewMarker(ScrewSlot slot, int step)
        {
            // La vis appartient à un pied : position relative à la racine de l'assemblage.
            var root = slot.transform.parent != null ? AssemblyRoot(slot.transform.parent) : slot.transform;
            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(m.GetComponent<Collider>());
            m.name = slot.name;
            m.transform.SetParent(container, false);
            m.transform.localPosition = root.InverseTransformPoint(slot.transform.position - slot.transform.forward * 0.012f) - new Vector3(0f, 0.24f, 0f);
            m.transform.localScale = Vector3.one * 0.025f;
            var r = m.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            items.Add(new Item { step = step, isScrew = true, go = m, renderers = new[] { r } });
        }

        // ---------------- Affichage d'une étape ----------------

        public void Show(int stepIndex)
        {
            if (manager == null || manager.Steps.Count == 0) return;
            shownStep = Mathf.Clamp(stepIndex, 0, manager.Steps.Count - 1);
            var step = manager.Steps[shownStep];

            foreach (var it in items)
            {
                bool tightenNow = it.isScrew && it.tightenStep == shownStep;
                bool visible = it.step <= shownStep;
                it.go.SetActive(visible);
                if (!visible) continue;
                var mat = (it.step == shownStep || tightenNow) ? (it.isScrew ? screwMaterial : currentMaterial) : doneMaterial;
                foreach (var r in it.renderers) if (r) r.sharedMaterial = mat;
            }

            bool preview = shownStep != manager.CurrentStepIndex && !manager.IsFinished;
            if (title)
                title.text = $"Notice — étape {shownStep + 1}/{manager.Steps.Count}" + (preview ? "  <color=#8FB8FF>(aperçu)</color>" : "");
            if (details)
            {
                string screws = step.expectedScrewCount <= 0 ? "Aucune vis."
                    : shownStep == manager.Steps.Count - 1 ? $"Serrer les {step.expectedScrewCount} vis à la clé Allen, puis contrôler l'équerre."
                    : $"{step.expectedScrewCount} vis à engager, sans les serrer.";
                details.text = $"<b>{step.title}</b>\n{screws}\n<size=80%><color=#8FB8FF>Notice papier IKEA AA-2547698-1, page {step.noticePage}</color></size>\n" +
                               "<size=80%><color=#FFCC1A>Jaune</color> : à faire dans cette étape · <color=#9AA0A6>gris</color> : déjà monté</size>";
            }
            if (previousButton) previousButton.interactable = shownStep > 0;
            if (nextButton) nextButton.interactable = shownStep < manager.Steps.Count - 1;
            if (currentButton) currentButton.interactable = preview;
        }
    }
}
