namespace WindowResizer.Base.Coordinators;

public sealed class WindowRestoreResult
{
    public WindowRestoreResult(
        bool matchedConfig,
        bool placementRestored,
        string? matchedRuleTitle,
        VirtualDesktopMoveStatus virtualDesktopMoveStatus,
        string? errorMessage,
        string? virtualDesktopMoveErrorMessage)
    {
        MatchedConfig = matchedConfig;
        PlacementRestored = placementRestored;
        MatchedRuleTitle = matchedRuleTitle;
        VirtualDesktopMoveStatus = virtualDesktopMoveStatus;
        ErrorMessage = errorMessage;
        VirtualDesktopMoveErrorMessage = virtualDesktopMoveErrorMessage;
    }

    public bool MatchedConfig { get; }

    public bool PlacementRestored { get; }

    public string? MatchedRuleTitle { get; }

    public VirtualDesktopMoveStatus VirtualDesktopMoveStatus { get; }

    public string? ErrorMessage { get; }

    public string? VirtualDesktopMoveErrorMessage { get; }
}
