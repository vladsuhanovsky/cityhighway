namespace CityHighways.Presentation
{
    /// <summary>
    /// Separate UI policy for entering map targeting. Never allow a synthetic
    /// selection during a bot turn, a handoff, a modal or a blocking popup.
    /// </summary>
    public static class GamepadMapTargetPolicy
    {
        public static bool AllowsInput(bool humanTurn, bool blockingOverlay,
            bool blockingMenu, bool handoff, bool animationBusy) =>
            humanTurn && !blockingOverlay && !blockingMenu &&
            !handoff && !animationBusy;
    }
}
