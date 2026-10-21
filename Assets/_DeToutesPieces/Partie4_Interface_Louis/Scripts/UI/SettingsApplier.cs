using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Feedback;
using UnityEngine;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Applique les réglages utilisateur : volume et vibrations au service de retours,
    /// taille des textes à tous les panneaux en espace monde (agrandissement proportionnel).
    /// </summary>
    public class SettingsApplier : MonoBehaviour
    {
        private readonly Dictionary<Canvas, Vector3> baseScales = new Dictionary<Canvas, Vector3>();

        private void OnEnable() => SessionSettings.Changed += Apply;
        private void OnDisable() => SessionSettings.Changed -= Apply;

        private void Start() => Apply();

        // Les écrans construits par code apparaissent après le démarrage : réapplication légère.
        private float nextScan;
        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1f;
            ApplyTextScale();
        }

        private void Apply()
        {
            var fb = FeedbackService.Instance;
            if (fb != null)
            {
                fb.masterVolume = SessionSettings.Volume;
                fb.soundEnabled = SessionSettings.Volume > 0.001f;
                fb.hapticIntensity = SessionSettings.Haptics ? 1f : 0f;
            }
            ApplyTextScale();
        }

        private void ApplyTextScale()
        {
            float k = SessionSettings.TextScale;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.renderMode != RenderMode.WorldSpace || !c.isRootCanvas) continue;
                if (!baseScales.TryGetValue(c, out var b)) baseScales[c] = b = c.transform.localScale;
                var target = b * k;
                if (c.transform.localScale != target) c.transform.localScale = target;
            }
        }
    }
}
