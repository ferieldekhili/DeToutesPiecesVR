using System.Collections.Generic;
using DeToutesPieces.Feedback;
using UnityEngine;

namespace DeToutesPieces.Parts
{
    /// <summary>
    /// Élément capable de réagir à la dépose d'une pièce (silhouette fantôme, emplacement de vis...).
    /// </summary>
    public interface IPartReleaseHandler
    {
        /// <summary>Vrai si ce gestionnaire est concerné par la dépose ; distance utilisée pour départager.</summary>
        bool CanConsider(AssemblyPart part, out float distance);

        /// <summary>Traite la dépose (placement, refus motivé...). Retourne vrai si un retour a été donné.</summary>
        bool HandleRelease(AssemblyPart part, IHapticChannel hand);
    }

    /// <summary>
    /// Confie la dépose d'une pièce au gestionnaire actif le plus proche (un seul retour par dépose).
    /// </summary>
    public static class PartReleaseRouter
    {
        private static readonly List<IPartReleaseHandler> Handlers = new List<IPartReleaseHandler>();

        public static void Register(IPartReleaseHandler h)
        {
            if (h != null && !Handlers.Contains(h)) Handlers.Add(h);
        }

        public static void Unregister(IPartReleaseHandler h) => Handlers.Remove(h);

        public static bool Resolve(AssemblyPart part, IHapticChannel hand)
        {
            IPartReleaseHandler best = null;
            float bestDist = float.MaxValue;
            for (int i = Handlers.Count - 1; i >= 0; i--)
            {
                var h = Handlers[i];
                if (h == null || (h is Object o && o == null)) { Handlers.RemoveAt(i); continue; }
                if (h.CanConsider(part, out float d) && d < bestDist) { best = h; bestDist = d; }
            }
            return best != null && best.HandleRelease(part, hand);
        }
    }
}
