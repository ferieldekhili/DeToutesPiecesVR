using System.Collections;
using DeToutesPieces.Core;
using DeToutesPieces.Interaction;
using DeToutesPieces.Parts;
using UnityEngine;

namespace DeToutesPieces.Guidance
{
    /// <summary>
    /// Indices progressifs (document V1, section 4) déclenchés par le bouton A :
    /// niveau 1 = texte (panneau), niveau 2 = surbrillance jaune de l'élément à utiliser,
    /// niveau 3 = courte démonstration : une copie translucide de l'élément rejoint sa position cible.
    /// La démonstration s'arrête avant la réalisation : l'utilisateur reprend la main et fait le geste.
    /// </summary>
    public class HintPresenter : MonoBehaviour
    {
        [SerializeField] private Material demonstrationMaterial;
        [SerializeField] private float highlightSeconds = 6f;
        [SerializeField] private float travelSeconds = 1.6f;
        [SerializeField] private int repetitions = 2;

        private Coroutine highlightRoutine;
        private PartHighlighter highlighted;
        private GameObject demo;

        private void Start()
        {
            if (AssemblyManager.Instance != null) AssemblyManager.Instance.HintRequested += OnHint;
        }

        private void OnDestroy()
        {
            if (AssemblyManager.Instance != null) AssemblyManager.Instance.HintRequested -= OnHint;
        }

        private void OnHint(AssemblyManager.HintLevel level, string text, StepRequirement req)
        {
            if (req == null || level < AssemblyManager.HintLevel.Highlight) return;
            var source = SourceFor(req);
            if (source == null) return;

            Highlight(source);
            if (level >= AssemblyManager.HintLevel.Demonstration && TargetPose(req, source, out var pos, out var rot, out var spinAxis))
                Demonstrate(source, pos, rot, spinAxis);
        }

        // ---------------- Élément concerné ----------------

        private static AssemblyPart SourceFor(StepRequirement req)
        {
            if (req is TightenRequirement)
            {
                var key = FindFirstObjectByType<AllenKey>();
                return key != null ? key.GetComponent<AssemblyPart>() : null;
            }
            foreach (var p in FindObjectsByType<AssemblyPart>(FindObjectsSortMode.InstanceID))
            {
                if (p.State != AssemblyPart.PartState.Free || PracticeItem.IsPractice(p)) continue;
                if (req is GhostSnapTarget g && p.definition == g.ExpectedPart) return p;
                if (req is ScrewSlot s && p.TryGetComponent(out FastenerItem f) && (s.ExpectedFastener == null || f.definition == s.ExpectedFastener)) return p;
            }
            return null;
        }

        private static bool TargetPose(StepRequirement req, AssemblyPart source, out Vector3 pos, out Quaternion rot, out Vector3 spinAxis)
        {
            spinAxis = Vector3.zero;
            switch (req)
            {
                case GhostSnapTarget g:
                    pos = g.transform.position; rot = g.transform.rotation;
                    return true;
                case ScrewSlot s:
                    rot = s.transform.rotation * Quaternion.FromToRotation(Vector3.up, Vector3.back);
                    pos = s.transform.position - s.transform.forward * 0.006f;
                    return true;
                case TightenRequirement t when t.Slot != null:
                    var tip = source.transform.Find("Embout");
                    rot = tip != null ? Quaternion.FromToRotation(tip.forward, t.Slot.transform.forward) * source.transform.rotation : source.transform.rotation;
                    var tipLocal = tip != null ? tip.localPosition : Vector3.zero;
                    pos = t.Slot.HeadPosition - rot * tipLocal;
                    spinAxis = -t.Slot.transform.forward; // sens horaire vu par l'utilisateur
                    return true;
            }
            pos = Vector3.zero; rot = Quaternion.identity;
            return false;
        }

        // ---------------- Niveau 2 : surbrillance ----------------

        private void Highlight(AssemblyPart part)
        {
            if (!part.TryGetComponent(out PartHighlighter hl)) return;
            if (highlightRoutine != null) StopCoroutine(highlightRoutine);
            ClearHighlight();
            highlighted = hl;
            hl.SetState(PartHighlighter.HighlightState.Hint);
            highlightRoutine = StartCoroutine(ClearLater());
        }

        private IEnumerator ClearLater()
        {
            yield return new WaitForSeconds(highlightSeconds);
            ClearHighlight();
        }

        private void ClearHighlight()
        {
            if (highlighted != null && highlighted.State == PartHighlighter.HighlightState.Hint)
                highlighted.SetState(PartHighlighter.HighlightState.None);
            highlighted = null;
        }

        // ---------------- Niveau 3 : démonstration ----------------

        private void Demonstrate(AssemblyPart source, Vector3 targetPos, Quaternion targetRot, Vector3 spinAxis)
        {
            if (demo != null) Destroy(demo);
            demo = CloneVisual(source.transform, demonstrationMaterial);
            StartCoroutine(Animate(demo, source.transform.position, source.transform.rotation, targetPos, targetRot, spinAxis));
        }

        private IEnumerator Animate(GameObject go, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, Vector3 spinAxis)
        {
            for (int r = 0; r < repetitions && go != null; r++)
            {
                float t = 0f;
                while (t < 1f && go != null)
                {
                    t += Time.deltaTime / travelSeconds;
                    float k = Mathf.SmoothStep(0f, 1f, t);
                    go.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.12f,
                        Quaternion.Slerp(fromRot, toRot, k));
                    yield return null;
                }
                if (spinAxis != Vector3.zero)
                {
                    // Clé : quelques quarts de tour dans le bon sens.
                    float spun = 0f;
                    while (spun < 180f && go != null)
                    {
                        float d = 180f * Time.deltaTime;
                        go.transform.RotateAround(toPos, spinAxis, d);
                        spun += d;
                        yield return null;
                    }
                }
                yield return new WaitForSeconds(0.4f);
            }
            if (go != null) Destroy(go);
        }

        private static GameObject CloneVisual(Transform source, Material mat)
        {
            var root = new GameObject("Démonstration (indice)");
            root.transform.SetPositionAndRotation(source.position, source.rotation);
            foreach (var mr in source.GetComponentsInChildren<MeshRenderer>())
            {
                if (!mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var c = new GameObject(mr.name);
                c.transform.SetParent(root.transform, false);
                c.transform.localPosition = source.InverseTransformPoint(mr.transform.position);
                c.transform.localRotation = Quaternion.Inverse(source.rotation) * mr.transform.rotation;
                c.transform.localScale = mr.transform.lossyScale;
                c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = c.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat != null ? mat : mr.sharedMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }
    }
}
