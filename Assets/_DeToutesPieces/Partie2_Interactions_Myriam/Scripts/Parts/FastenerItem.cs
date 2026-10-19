using DeToutesPieces.Data;
using UnityEngine;

namespace DeToutesPieces.Parts
{
    /// <summary>
    /// Marque une pièce de visserie (vis du kit). Utilisé avec AssemblyPart (saisie, retour au bac)
    /// et reconnu par les emplacements de vis (ScrewSlot) qui vérifient la référence.
    /// </summary>
    [RequireComponent(typeof(AssemblyPart))]
    public class FastenerItem : MonoBehaviour
    {
        public FastenerDefinition definition;

        [Tooltip("Longueur de vis dépassant de la surface lorsque la vis est engagée mais non serrée (m).")]
        public float looseProtrusion = 0.006f;

        public AssemblyPart Part { get; private set; }

        private void Awake() => Part = GetComponent<AssemblyPart>();
    }
}
