using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeToutesPieces.Core
{
    /// <summary>
    /// Mesures de session pour le bilan (temps actif, erreurs, aides) — sans note ni classement.
    /// </summary>
    [Serializable]
    public class SessionStats
    {
        public float activeTimeSeconds;
        public int placementErrors;
        public int fasteningErrors;
        public int hintsRequested;
        public int undoCount;
        public int partsReturned;
        public List<int> stepsCompletedWithoutHint = new List<int>();

        private int hintsAtStepStart;

        public void Tick(float dt) => activeTimeSeconds += dt;

        public void OnStepStarted() => hintsAtStepStart = hintsRequested;

        public void OnStepCompleted(int stepIndex)
        {
            if (hintsRequested == hintsAtStepStart) stepsCompletedWithoutHint.Add(stepIndex);
        }

        public string FormatActiveTime()
        {
            var t = TimeSpan.FromSeconds(Mathf.Max(0f, activeTimeSeconds));
            return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        }
    }
}
