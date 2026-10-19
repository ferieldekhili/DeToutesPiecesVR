using System;
using UnityEngine;

namespace DeToutesPieces.Core
{
    /// <summary>
    /// Condition élémentaire d'une étape de montage (une pièce placée, une vis serrée...).
    /// Une étape est validée lorsque toutes ses conditions sont satisfaites.
    /// Les comportements concrets (GhostSnapTarget, ScrewSlot...) héritent de cette classe.
    /// </summary>
    public abstract class StepRequirement : MonoBehaviour
    {
        public event Action<StepRequirement> StateChanged;

        /// <summary>Vrai lorsque la condition est remplie.</summary>
        public abstract bool IsSatisfied { get; }

        /// <summary>Libellé court pour le panneau de progression (ex. « Placer le plateau »).</summary>
        public abstract string Label { get; }

        /// <summary>Point visé par la flèche de guidage tant que la condition n'est pas remplie.</summary>
        public virtual Transform GuidanceTarget => transform;

        /// <summary>Direction (monde) dans laquelle la flèche pointe vers la cible (par défaut : vers le bas).</summary>
        public virtual Vector3 GuidanceDirection => Vector3.down;

        /// <summary>Vrai si la condition appartient à l'étape en cours (seules celles-ci réagissent).</summary>
        public bool IsActiveForStep { get; private set; }

        public virtual void SetActiveForStep(bool active)
        {
            IsActiveForStep = active;
        }

        protected void RaiseStateChanged() => StateChanged?.Invoke(this);
    }

    /// <summary>Action réversible enregistrée dans la pile d'annulation (bouton B).</summary>
    public interface IUndoableAction
    {
        string Label { get; }
        void Undo();
    }
}
