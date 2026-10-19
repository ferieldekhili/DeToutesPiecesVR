using UnityEngine;

namespace DeToutesPieces.Data
{
    /// <summary>
    /// Référence de visserie du kit. Chaque référence réelle de la notice correspond à un asset.
    /// Le système d'identification des vis (couleur, étiquette, gabarit 1:1) s'appuie uniquement sur ces assets :
    /// aucune référence fictive ne doit être ajoutée sans être signalée (champ verifiedAgainstNotice).
    /// </summary>
    [CreateAssetMenu(menuName = "De Toutes Pièces/Définition de visserie", fileName = "Fastener_")]
    public class FastenerDefinition : ScriptableObject
    {
        [Tooltip("Référence affichée (ex. numéro IKEA de la notice).")]
        public string reference;

        public string displayName = "Vis";

        [Tooltip("Longueur en millimètres (0 = inconnue, à relever sur la notice p. 6).")]
        [Min(0)] public float lengthMm;

        [Tooltip("Diamètre en millimètres (0 = inconnu).")]
        [Min(0)] public float diameterMm;

        [Min(0)] public int quantityInKit;

        [Tooltip("Couleur d'identification utilisée dans la fenêtre d'outils et sur les trous cibles.")]
        public Color identificationColor = new Color(1f, 0.8f, 0.1f);

        [Tooltip("Faux tant que les valeurs n'ont pas été vérifiées sur la notice papier.")]
        public bool verifiedAgainstNotice;

        public string LengthLabel => lengthMm > 0 ? $"{lengthMm:0} mm" : "longueur à vérifier";
    }
}
