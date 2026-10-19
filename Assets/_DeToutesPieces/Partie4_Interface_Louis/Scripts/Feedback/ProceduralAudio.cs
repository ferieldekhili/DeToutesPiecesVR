using UnityEngine;

namespace DeToutesPieces.Feedback
{
    /// <summary>
    /// Sons provisoires générés par code (bips, clics, sons métalliques) pour que chaque action ait un retour
    /// sonore dès le prototype. À remplacer par des AudioClip définitifs dans FeedbackService.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        public static AudioClip Tone(string name, float freq, float duration, float decay)
        {
            int n = Mathf.Max(1, (int)(SampleRate * duration));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t / Mathf.Max(0.001f, duration * decay)) * Attack(i);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
            }
            return Make(name, data);
        }

        public static AudioClip Chime(string name, float[] freqs, float noteDuration)
        {
            int perNote = (int)(SampleRate * noteDuration);
            int tail = (int)(SampleRate * 0.15f);
            var data = new float[perNote * freqs.Length + tail];
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = k * perNote;
                int len = perNote + tail;
                for (int i = 0; i < len && start + i < data.Length; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Mathf.Exp(-t * 9f) * Attack(i);
                    data[start + i] += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * env * 0.35f;
                }
            }
            return Make(name, data);
        }

        public static AudioClip Buzz(string name, float freq, float duration)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t));
                float env = (1f - (float)i / n) * Attack(i);
                data[i] = square * env * 0.25f;
            }
            return Make(name, data);
        }

        public static AudioClip Click(string name, float duration)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Exp(-(float)i / (n * 0.15f));
                data[i] = ((float)rng.NextDouble() * 2f - 1f) * env * 0.5f;
            }
            return Make(name, data);
        }

        public static AudioClip Metal(string name, float duration)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            float[] partials = { 2100f, 3350f, 5200f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 25f) * Attack(i);
                float v = 0f;
                foreach (var p in partials) v += Mathf.Sin(2f * Mathf.PI * p * t);
                data[i] = v / partials.Length * env * 0.5f;
            }
            return Make(name, data);
        }

        private static float Attack(int i) => Mathf.Clamp01(i / (SampleRate * 0.004f));

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create("Proc_" + name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
