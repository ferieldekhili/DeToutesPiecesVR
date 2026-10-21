using System;
using UnityEngine;

namespace DeToutesPieces.Core
{
    public enum Handedness { Right, Left }
    public enum WorkPosture { Standing, Seated }

    /// <summary>
    /// Réglages utilisateur persistants (document V1, section 5 : confort et accessibilité) :
    /// main dominante, posture, volume, vibrations, taille des textes. Toutes les parties les lisent
    /// et réagissent à l'événement Changed.
    /// </summary>
    public static class SessionSettings
    {
        private const string Prefix = "dtp.reglages.";
        public static event Action Changed;

        public static Handedness DominantHand
        {
            get => (Handedness)PlayerPrefs.GetInt(Prefix + "main", 0);
            set { PlayerPrefs.SetInt(Prefix + "main", (int)value); Notify(); }
        }

        public static WorkPosture Posture
        {
            get => (WorkPosture)PlayerPrefs.GetInt(Prefix + "posture", 0);
            set { PlayerPrefs.SetInt(Prefix + "posture", (int)value); Notify(); }
        }

        /// <summary>Volume général (0 à 1).</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(Prefix + "volume", 1f);
            set { PlayerPrefs.SetFloat(Prefix + "volume", Mathf.Clamp01(value)); Notify(); }
        }

        public static bool Haptics
        {
            get => PlayerPrefs.GetInt(Prefix + "vibrations", 1) == 1;
            set { PlayerPrefs.SetInt(Prefix + "vibrations", value ? 1 : 0); Notify(); }
        }

        /// <summary>Facteur de taille des panneaux et textes (0,8 à 1,4).</summary>
        public static float TextScale
        {
            get => PlayerPrefs.GetFloat(Prefix + "texte", 1f);
            set { PlayerPrefs.SetFloat(Prefix + "texte", Mathf.Clamp(value, 0.8f, 1.4f)); Notify(); }
        }

        /// <summary>Vrai si la prise en main a déjà été suivie : elle peut alors être passée.</summary>
        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt(Prefix + "tutoriel", 0) == 1;
            set { PlayerPrefs.SetInt(Prefix + "tutoriel", value ? 1 : 0); Notify(); }
        }

        private static void Notify()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Sauvegarde locale du dernier jalon complet (document V1 : « la progression est enregistrée à chaque
    /// étape validée » ; après une interruption, la reprise repart de ce jalon).
    /// </summary>
    public static class ProgressSave
    {
        private const string Key = "dtp.progression.etape";

        /// <summary>Index de la prochaine étape à réaliser, ou -1 s'il n'y a rien à reprendre.</summary>
        public static int SavedStep => PlayerPrefs.GetInt(Key, -1);
        public static bool HasSave => SavedStep > 0;

        public static void Save(int nextStepIndex)
        {
            PlayerPrefs.SetInt(Key, nextStepIndex);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
