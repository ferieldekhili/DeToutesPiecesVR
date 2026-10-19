using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeToutesPieces.Feedback
{
    /// <summary>
    /// Point d'entrée unique des retours perceptibles : son localisé, impulsion haptique, pulsation visuelle
    /// et message texte (toujours doublé à l'écran : aucune information n'est transmise par le son seul).
    /// Les sons sont générés procéduralement tant qu'aucun AudioClip définitif n'est assigné.
    /// </summary>
    public class FeedbackService : MonoBehaviour
    {
        public static FeedbackService Instance { get; private set; }

        // Code couleur : FeedbackPalette (Partie 1).

        [Serializable]
        public class KindSettings
        {
            public FeedbackKind kind;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.6f;
            [Range(0f, 1f)] public float hapticAmplitude = 0.3f;
            [Min(0f)] public float hapticDuration = 0.08f;
            public bool visualPulse;
        }

        [Header("Réglages par type de retour (optionnel)")]
        [SerializeField] private List<KindSettings> overrides = new List<KindSettings>();

        [Header("Préférences utilisateur")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float hapticIntensity = 1f;
        public bool soundEnabled = true;

        [Header("Pulsation visuelle")]
        [SerializeField] private Material pulseMaterial;
        [SerializeField] private float pulseDuration = 0.45f;
        [SerializeField] private float pulseMaxScale = 0.12f;

        /// <summary>Message destiné au panneau de consigne (texte, couleur sémantique).</summary>
        public event Action<FeedbackKind, string, Color> MessageRaised;

        private readonly Dictionary<FeedbackKind, KindSettings> settings = new Dictionary<FeedbackKind, KindSettings>();
        private readonly Dictionary<FeedbackKind, AudioClip> generatedClips = new Dictionary<FeedbackKind, AudioClip>();
        private readonly Queue<AudioSource> sourcePool = new Queue<AudioSource>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildDefaults();
        }

        // Les autres parties publient sur FeedbackBus (noyau) ; ce service produit le rendu.
        private void OnEnable() => FeedbackBus.Raised += OnFeedback;
        private void OnDisable() => FeedbackBus.Raised -= OnFeedback;
        private void OnFeedback(FeedbackEvent e) => Play(e.kind, e.position, e.message, e.haptic);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildDefaults()
        {
            settings.Clear();
            void Def(FeedbackKind k, float vol, float amp, float dur, bool pulse) =>
                settings[k] = new KindSettings { kind = k, volume = vol, hapticAmplitude = amp, hapticDuration = dur, visualPulse = pulse };

            Def(FeedbackKind.Hover, 0.15f, 0.05f, 0.02f, false);
            Def(FeedbackKind.Grab, 0.35f, 0.25f, 0.05f, false);
            Def(FeedbackKind.Release, 0.2f, 0.1f, 0.03f, false);
            Def(FeedbackKind.PlacementPreviewValid, 0.2f, 0.12f, 0.03f, false);
            Def(FeedbackKind.PlacementValid, 0.6f, 0.5f, 0.10f, true);
            Def(FeedbackKind.PlacementInvalid, 0.55f, 0.6f, 0.18f, true);
            Def(FeedbackKind.ScrewEngaged, 0.5f, 0.35f, 0.06f, false);
            Def(FeedbackKind.ScrewProgress, 0.25f, 0.12f, 0.03f, false);
            Def(FeedbackKind.ScrewTightened, 0.6f, 0.7f, 0.12f, true);
            Def(FeedbackKind.WrongAction, 0.5f, 0.5f, 0.15f, true);
            Def(FeedbackKind.StepReady, 0.6f, 0.3f, 0.10f, false);
            Def(FeedbackKind.StepCompleted, 0.7f, 0.4f, 0.15f, false);
            Def(FeedbackKind.AssemblyCompleted, 0.8f, 0.5f, 0.25f, false);
            Def(FeedbackKind.PartReturned, 0.35f, 0f, 0f, false);
            Def(FeedbackKind.UndoPerformed, 0.4f, 0.2f, 0.05f, false);
            Def(FeedbackKind.Info, 0.3f, 0f, 0f, false);

            foreach (var o in overrides)
                if (o != null) settings[o.kind] = o;
        }

        /// <summary>Déclenche un retour complet. Toutes les entrées sont optionnelles.</summary>
        public void Play(FeedbackKind kind, Vector3? worldPosition = null, string message = null, IHapticChannel haptic = null)
        {
            if (!settings.TryGetValue(kind, out var s)) return;

            if (soundEnabled && s.volume > 0f)
                PlaySound(kind, s, worldPosition);

            if (haptic != null && s.hapticAmplitude > 0f && hapticIntensity > 0f)
                haptic.Pulse(s.hapticAmplitude * hapticIntensity, s.hapticDuration);

            if (s.visualPulse && worldPosition.HasValue && pulseMaterial != null)
                StartCoroutine(PulseRoutine(worldPosition.Value, ColorFor(kind)));

            if (!string.IsNullOrEmpty(message))
                MessageRaised?.Invoke(kind, message, ColorFor(kind));
        }

        public static Color ColorFor(FeedbackKind kind) => FeedbackPalette.ColorFor(kind);

        // ---------- Audio ----------

        private void PlaySound(FeedbackKind kind, KindSettings s, Vector3? position)
        {
            var clip = s.clip != null ? s.clip : GetGeneratedClip(kind);
            if (clip == null) return;

            var src = GetSource();
            // Sons d'action spatialisés à la source ; notifications centrées (document, section 2).
            bool spatial = position.HasValue && IsActionSound(kind);
            src.spatialBlend = spatial ? 1f : 0f;
            src.transform.position = position ?? transform.position;
            src.volume = s.volume * masterVolume;
            src.clip = clip;
            src.Play();
            StartCoroutine(ReturnSourceWhenDone(src, clip.length));
        }

        private static bool IsActionSound(FeedbackKind k) =>
            k == FeedbackKind.Grab || k == FeedbackKind.Release || k == FeedbackKind.PlacementValid ||
            k == FeedbackKind.PlacementInvalid || k == FeedbackKind.ScrewEngaged || k == FeedbackKind.ScrewProgress ||
            k == FeedbackKind.ScrewTightened || k == FeedbackKind.WrongAction || k == FeedbackKind.PlacementPreviewValid;

        private AudioSource GetSource()
        {
            if (sourcePool.Count > 0) return sourcePool.Dequeue();
            var go = new GameObject("FeedbackAudio");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 0.3f;
            src.maxDistance = 6f;
            return src;
        }

        private IEnumerator ReturnSourceWhenDone(AudioSource src, float delay)
        {
            yield return new WaitForSeconds(delay + 0.05f);
            if (src != null) { src.Stop(); sourcePool.Enqueue(src); }
        }

        private AudioClip GetGeneratedClip(FeedbackKind kind)
        {
            if (generatedClips.TryGetValue(kind, out var c)) return c;
            switch (kind)
            {
                case FeedbackKind.Hover: c = ProceduralAudio.Tone("hover", 1400f, 0.03f, 0.3f); break;
                case FeedbackKind.Grab: c = ProceduralAudio.Click("grab", 0.05f); break;
                case FeedbackKind.Release: c = ProceduralAudio.Click("release", 0.04f); break;
                case FeedbackKind.PlacementPreviewValid: c = ProceduralAudio.Tone("preview", 880f, 0.05f, 0.4f); break;
                case FeedbackKind.PlacementValid: c = ProceduralAudio.Chime("placeOk", new[] { 660f, 990f }, 0.09f); break;
                case FeedbackKind.PlacementInvalid: c = ProceduralAudio.Buzz("placeKo", 160f, 0.25f); break;
                case FeedbackKind.ScrewEngaged: c = ProceduralAudio.Metal("engage", 0.12f); break;
                case FeedbackKind.ScrewProgress: c = ProceduralAudio.Click("ratchet", 0.03f); break;
                case FeedbackKind.ScrewTightened: c = ProceduralAudio.Metal("tight", 0.2f); break;
                case FeedbackKind.WrongAction: c = ProceduralAudio.Buzz("wrong", 200f, 0.2f); break;
                case FeedbackKind.StepReady: c = ProceduralAudio.Chime("ready", new[] { 523f, 659f, 784f }, 0.1f); break;
                case FeedbackKind.StepCompleted: c = ProceduralAudio.Chime("step", new[] { 523f, 784f, 1046f }, 0.12f); break;
                case FeedbackKind.AssemblyCompleted: c = ProceduralAudio.Chime("done", new[] { 523f, 659f, 784f, 1046f }, 0.16f); break;
                case FeedbackKind.PartReturned: c = ProceduralAudio.Tone("return", 440f, 0.12f, 0.5f); break;
                case FeedbackKind.UndoPerformed: c = ProceduralAudio.Chime("undo", new[] { 784f, 523f }, 0.08f); break;
                default: c = ProceduralAudio.Tone("info", 700f, 0.08f, 0.4f); break;
            }
            generatedClips[kind] = c;
            return c;
        }

        // ---------- Visuel ----------

        private IEnumerator PulseRoutine(Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FeedbackPulse";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = pulseMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            float t = 0f;
            while (t < pulseDuration)
            {
                float k = t / pulseDuration;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.02f, pulseMaxScale, k);
                var c = color; c.a = Mathf.Lerp(0.6f, 0f, k);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
                t += Time.deltaTime;
                yield return null;
            }
            Destroy(go);
        }
    }
}
