using System.Collections.Generic;
using WindowResizer.Base.Coordinators;
using Xunit;

namespace WindowResizer.Base.Tests;

public class LayoutSnapshotOperationFormatterTests
{
    [Fact]
    public void FormatSaveSummary_IncludesCloakedCountWhenPresent()
    {
        var result = new LayoutSnapshotSaveResult(savedWindowCount: 3, desktopCount: 2, cloakedWindowCount: 1);

        var message = LayoutSnapshotOperationFormatter.FormatSaveSummary(result);

        Assert.Equal("Saved 3 windows across 2 desktops. Cloaked 1.", message);
    }

    [Fact]
    public void FormatRestoreSummary_IncludesAdminHintWhenPlacementAccessDeniedOccurs()
    {
        var result = new LayoutSnapshotRestoreResult(
            noSnapshot: false,
            restoredCount: 22,
            moveFallbackCount: 0,
            unmatchedCount: 0,
            failedCount: 1,
            failedEntries: new[]
            {
                "v2rayN.exe :: Admin Window :: HwndWrapper [SetWindowPlacement failed for 123: Access is denied.]"
            });

        var message = LayoutSnapshotOperationFormatter.FormatRestoreSummary(result);

        Assert.Equal("Restored 22. Move fallback 0. Unmatched 0. Failed 1.", message);
    }

    [Fact]
    public void FormatRestoreSummary_IncludesAdminHintWhenPlacementAccessDeniedOccursInChinese()
    {
        var result = new LayoutSnapshotRestoreResult(
            noSnapshot: false,
            restoredCount: 22,
            moveFallbackCount: 0,
            unmatchedCount: 0,
            failedCount: 1,
            failedEntries: new[]
            {
                "v2rayN.exe :: Admin Window :: HwndWrapper [SetWindowPlacement failed for 123: 拒绝访问。]"
            });

        var message = LayoutSnapshotOperationFormatter.FormatRestoreSummary(result);

        Assert.Equal("Restored 22. Move fallback 0. Unmatched 0. Failed 1.", message);
    }

    [Fact]
    public void FormatRestoreSummary_AdminHintVariant_KeepsNonZeroSecondaryCounts()
    {
        var result = new LayoutSnapshotRestoreResult(
            noSnapshot: false,
            restoredCount: 20,
            moveFallbackCount: 1,
            unmatchedCount: 2,
            failedCount: 1,
            failedEntries: new[]
            {
                "admin.exe :: Admin Window :: Host [SetWindowPlacement failed for 123: Access is denied.]"
            });

        var message = LayoutSnapshotOperationFormatter.FormatRestoreSummary(result);

        Assert.Equal("Restored 20. Move fallback 1. Unmatched 2. Failed 1.", message);
    }

    [Fact]
    public void FormatRestoreSummary_DoesNotTreatTitleKeywordAsAdminFailure()
    {
        var result = new LayoutSnapshotRestoreResult(
            noSnapshot: false,
            restoredCount: 0,
            moveFallbackCount: 0,
            unmatchedCount: 0,
            failedCount: 1,
            failedEntries: new[]
            {
                "demo.exe :: Access is denied dashboard :: DemoClass [Unable to restore placement.]"
            });

        var message = LayoutSnapshotOperationFormatter.FormatRestoreSummary(result);

        Assert.Equal("Restored 0. Move fallback 0. Unmatched 0. Failed 1.", message);
    }

    [Fact]
    public void FormatRestoreSummary_RemainsCompactWhenNoPermissionSpecificFailureExists()
    {
        var result = new LayoutSnapshotRestoreResult(
            noSnapshot: false,
            restoredCount: 2,
            moveFallbackCount: 1,
            unmatchedCount: 1,
            failedCount: 0,
            moveFallbackEntries: new[] { "notepad.exe :: first.txt :: Notepad [Move failed]" },
            unmatchedEntries: new[] { "missing.exe :: third.txt :: Missing" });

        var message = LayoutSnapshotOperationFormatter.FormatRestoreSummary(result);

        Assert.Equal("Restored 2. Move fallback 1. Unmatched 1. Failed 0.", message);
    }

    [Fact]
    public void FormatRestoreLog_IncludesDetailLists()
    {
        var result = new LayoutSnapshotRestoreResult(
            noSnapshot: false,
            restoredCount: 1,
            moveFallbackCount: 1,
            unmatchedCount: 1,
            failedCount: 1,
            moveFallbackEntries: new[] { "notepad.exe :: first.txt :: Notepad" },
            unmatchedEntries: new[] { "missing.exe :: third.txt :: Missing" },
            failedEntries: new[] { "code.exe :: second.txt :: Chrome_WidgetWin_1 [Access is denied.]" });

        var message = LayoutSnapshotOperationFormatter.FormatRestoreLog(result);

        Assert.Contains("restored=1", message);
        Assert.Contains("moveFallback=1", message);
        Assert.Contains("unmatched=1", message);
        Assert.Contains("failed=1", message);
        Assert.Contains("Move fallback: notepad.exe :: first.txt :: Notepad.", message);
        Assert.Contains("Unmatched: missing.exe :: third.txt :: Missing.", message);
        Assert.Contains("Placement failed: code.exe :: second.txt :: Chrome_WidgetWin_1 [Access is denied.].", message);
    }
}
