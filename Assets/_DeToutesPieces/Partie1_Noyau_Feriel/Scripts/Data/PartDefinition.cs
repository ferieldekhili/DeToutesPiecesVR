using UnityEngine;

namespace DeToutesPieces.Data
{
    /// <summary>
    /// Description d'une pièce du kit (plateau, tablette, cadre...).
    /// Partagée entre l'inventaire, la notice virtuelle, la fenêtre d'outils et la validation des étapes.
    /// </summary>
    [CreateAssetMenu(menuName = "De Toutes Pièces/Définition de pièce", fileName = "Part_")]
    public class PartDefinition : ScriptableObject
    {
        [Tooltip("Identifiant unique (ex. KNV_PLATEAU).")]
        public string partId;

        [Tooltip("Nom affiché à l'utilisateur, en français.")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("Courte description affichée dans l'inventaire.")]
        public string description;

        [Min(1)] public int quantityInKit = 1;

        [Tooltip("Repère éventuel de la notice IKEA (à compléter d'après la notice AA-2547698-1).")]
        public string noticeReference;

        [Tooltip("Faux tant que la pièce n'a pas été vérifiée sur la notice papier.")]
        public bool verifiedAgainstNotice;

        public Sprite icon;
    }
}
