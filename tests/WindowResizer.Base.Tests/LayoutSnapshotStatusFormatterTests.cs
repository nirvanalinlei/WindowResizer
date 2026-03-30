using System;
using System.Collections.Generic;
using WindowResizer.Base;
using WindowResizer.Configuration;
using Xunit;

namespace WindowResizer.Base.Tests;

public class LayoutSnapshotStatusFormatterTests
{
    [Fact]
    public void Format_WhenSnapshotMissing_ReturnsEmptyStateMessage()
    {
        var text = LayoutSnapshotStatusFormatter.Format(null);

        Assert.Equal("Last layout snapshot: not saved yet.", text);
    }

    [Fact]
    public void Format_WhenSnapshotExists_ReturnsTimestampAndWindowCount()
    {
        var capturedAt = new DateTimeOffset(2026, 3, 28, 8, 30, 0, TimeSpan.Zero);
        var snapshot = new WindowLayoutSnapshot
        {
            CapturedAt = capturedAt,
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new(),
                new()
            }
        };

        var expectedTimestamp = capturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        var text = LayoutSnapshotStatusFormatter.Format(snapshot);

        Assert.Equal($"Last layout snapshot: {expectedTimestamp} (2 windows).", text);
    }

    [Fact]
    public void Format_WhenSnapshotExistsButEmpty_ReturnsSavedTimestampAndZeroCount()
    {
        var capturedAt = new DateTimeOffset(2026, 3, 28, 9, 45, 0, TimeSpan.Zero);
        var snapshot = new WindowLayoutSnapshot
        {
            CapturedAt = capturedAt
        };

        var expectedTimestamp = capturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        var text = LayoutSnapshotStatusFormatter.Format(snapshot);

        Assert.Equal($"Last layout snapshot: {expectedTimestamp} (0 windows).", text);
    }
}
