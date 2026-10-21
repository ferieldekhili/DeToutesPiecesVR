using UnityEngine;

namespace DeToutesPieces.Parts
{
    /// <summary>
    /// Marque un objet d'exercice de la prise en main (cube, vis d'entraînement).
    /// Ces objets ne font pas partie du kit : l'inventaire, la fenêtre d'outils et la reprise les ignorent.
    /// </summary>
    public class PracticeItem : MonoBehaviour
    {
        /// <summary>Vrai si l'objet appartient à la zone d'exercice (marqueur sur lui-même ou un parent).</summary>
        public static bool IsPractice(Component c) => c != null && c.GetComponentInParent<PracticeItem>(true) != null;
    }
}
