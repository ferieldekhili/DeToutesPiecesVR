namespace DeToutesPieces.Feedback
{
    /// <summary>Événements perceptibles de l'application (retours visuels, sonores et haptiques).</summary>
    public enum FeedbackKind
    {
        Hover,
        Grab,
        Release,
        PlacementPreviewValid,
        PlacementValid,
        PlacementInvalid,
        ScrewEngaged,
        ScrewProgress,
        ScrewTightened,
        WrongAction,
        StepReady,
        StepCompleted,
        AssemblyCompleted,
        PartReturned,
        UndoPerformed,
        Info
    }

    /// <summary>Canal haptique (une manette). Implémenté par l'adaptateur XR, ignoré sans casque.</summary>
    public interface IHapticChannel
    {
        void Pulse(float amplitude, float duration);
    }
}
