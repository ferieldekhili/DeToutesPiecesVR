using DeToutesPieces.Core;
using UnityEngine;

namespace DeToutesPieces.Parts
{
    /// <summary>
    /// Condition « vis serrée » de l'étape de serrage final (notice p. 12, 16 vis).
    /// Autorise le serrage de son trou uniquement pendant cette étape :
    /// aux étapes 1 à 5, la notice demande de laisser les vis desserrées.
    /// </summary>
    public class TightenRequirement : StepRequirement
    {
        [SerializeField] private ScrewSlot slot;

        public ScrewSlot Slot => slot;
        public override bool IsSatisfied => slot != null && slot.State == ScrewSlot.SlotState.Tightened;
        public override string Label => slot != null ? $"Serrer la vis ({slot.LocationLabel})" : "Serrer une vis";
        public override Transform GuidanceTarget => slot != null ? slot.transform : transform;
        public override Vector3 GuidanceDirection => slot != null ? slot.transform.forward : Vector3.down;

        private void OnEnable()
        {
            if (slot != null) slot.StateChanged += OnSlotChanged;
        }

        private void OnDisable()
        {
            if (slot != null) slot.StateChanged -= OnSlotChanged;
        }

        private void OnSlotChanged(StepRequirement _) => RaiseStateChanged();

        public override void SetActiveForStep(bool active)
        {
            base.SetActiveForStep(active);
            if (slot != null) slot.SetTighteningAllowed(active);
        }
    }
}
