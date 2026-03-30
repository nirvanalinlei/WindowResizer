namespace WindowResizer.Base.Coordinators;

public sealed class LayoutSnapshotSaveResult
{
    public LayoutSnapshotSaveResult(int savedWindowCount, int desktopCount, int cloakedWindowCount)
    {
        SavedWindowCount = savedWindowCount;
        DesktopCount = desktopCount;
        CloakedWindowCount = cloakedWindowCount;
    }

    public int SavedWindowCount { get; }

    public int DesktopCount { get; }

    public int CloakedWindowCount { get; }
}
