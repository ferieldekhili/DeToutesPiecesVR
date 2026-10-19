using System;
using UnityEngine;

namespace DeToutesPieces.Feedback
{
    /// <summary>Un retour perceptible demandé par la logique de montage.</summary>
    public struct FeedbackEvent
    {
        public FeedbackKind kind;
        public Vector3? position;
        public string message;
        public IHapticChannel haptic;
        public Color Color => FeedbackPalette.ColorFor(kind);
    }

    /// <summary>
    /// Canal de retours du noyau (Partie 1). La logique émet des retours sans connaître leur rendu ;
    /// le rendu son / haptique / visuel (FeedbackService, Partie 3) et l'affichage des messages
    /// (panneau, Partie 4) s'y abonnent. Aucune partie ne dépend ainsi directement d'une autre.
    /// </summary>
    public static class FeedbackBus
    {
        public static event Action<FeedbackEvent> Raised;

        public static void Emit(FeedbackKind kind, Vector3? worldPosition = null, string message = null, IHapticChannel haptic = null)
        {
            var e = new FeedbackEvent { kind = kind, position = worldPosition, message = message, haptic = haptic };
            if (Raised != null) Raised(e);
            else if (!string.IsNullOrEmpty(message)) Debug.Log($"[Retour:{kind}] {message}");
        }
    }

    /// <summary>Code couleur du document de conception (section 4), partagé par toutes les parties.</summary>
    public static class FeedbackPalette
    {
        public static readonly Color InfoBlue = new Color(0.20f, 0.55f, 1f);
        public static readonly Color SuccessGreen = new Color(0.20f, 0.85f, 0.35f);
        public static readonly Color ErrorRed = new Color(0.95f, 0.22f, 0.20f);
        public static readonly Color CommandYellow = new Color(1f, 0.80f, 0.10f);
        public static readonly Color HoverWhite = new Color(1f, 1f, 1f);

        public static Color ColorFor(FeedbackKind kind)
        {
            switch (kind)
            {
                case FeedbackKind.PlacementInvalid:
                case FeedbackKind.WrongAction:
                    return ErrorRed;
                case FeedbackKind.PlacementValid:
                case FeedbackKind.PlacementPreviewValid:
                case FeedbackKind.ScrewTightened:
                case FeedbackKind.StepReady:
                case FeedbackKind.StepCompleted:
                case FeedbackKind.AssemblyCompleted:
                    return SuccessGreen;
                case FeedbackKind.Hover:
                    return HoverWhite;
                default:
                    return InfoBlue;
            }
        }
    }
}
