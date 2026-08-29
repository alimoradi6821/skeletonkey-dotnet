using SkeletonKey.Runner.Core;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Tests standalone schedule boundary calculation across interval and daily schedules.</summary>
public sealed class StandaloneScheduleCursorTests
{
    /// <summary>Verifies interval schedules retain fixed boundaries and skip missed occurrences.</summary>
    [Fact]
    public void IntervalKeepsFixedBoundariesAndSkipsMissedOccurrences()
    {
        DateTimeOffset anchor = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);
        StandaloneScheduleCursor cursor = new(
            new StandaloneSchedule(StandaloneScheduleKind.Interval, TimeSpan.FromMinutes(5), null),
            anchor,
            TimeZoneInfo.Utc);

        Assert.Equal(anchor.AddMinutes(5), cursor.GetNextDueAfter(anchor));
        Assert.Equal(anchor.AddMinutes(15), cursor.GetNextDueAfter(anchor.AddMinutes(12)));
    }

    /// <summary>Verifies daily schedules use the configured local wall-clock time.</summary>
    [Fact]
    public void DailyUsesConfiguredLocalWallClock()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+0330", TimeSpan.FromMinutes(210), "UTC+0330", "UTC+0330");
        DateTimeOffset after = new(2026, 8, 29, 3, 0, 0, TimeSpan.Zero); // 06:30 local
        StandaloneScheduleCursor cursor = new(
            new StandaloneSchedule(StandaloneScheduleKind.Daily, null, new TimeOnly(8, 30)),
            after,
            zone);

        Assert.Equal(new DateTimeOffset(2026, 8, 29, 5, 0, 0, TimeSpan.Zero), cursor.GetNextDueAfter(after));
    }

    /// <summary>Verifies spring-forward gaps normalize to the first valid local minute.</summary>
    [Fact]
    public void DailyNormalizesSpringForwardGapToFirstValidMinute()
    {
        TimeZoneInfo zone = CreateDstZone();
        DateTimeOffset after = new(2026, 3, 8, 6, 0, 0, TimeSpan.Zero); // 01:00 standard local
        StandaloneScheduleCursor cursor = new(
            new StandaloneSchedule(StandaloneScheduleKind.Daily, null, new TimeOnly(2, 30)),
            after,
            zone);

        // 02:30 is invalid on the transition day; the implementation normalizes to 03:00 local = 07:00 UTC.
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero), cursor.GetNextDueAfter(after));
    }

    /// <summary>Verifies ambiguous fall-back times resolve to the earlier UTC occurrence.</summary>
    [Fact]
    public void DailyChoosesEarlierUtcOccurrenceForAmbiguousFallBackTime()
    {
        TimeZoneInfo zone = CreateDstZone();
        DateTimeOffset after = new(2026, 11, 1, 4, 0, 0, TimeSpan.Zero);
        StandaloneScheduleCursor cursor = new(
            new StandaloneSchedule(StandaloneScheduleKind.Daily, null, new TimeOnly(1, 30)),
            after,
            zone);

        // The larger (-04:00) offset is the earlier UTC occurrence: 05:30 UTC.
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), cursor.GetNextDueAfter(after));
    }

    private static TimeZoneInfo CreateDstZone()
    {
        var daylightStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            3,
            2,
            DayOfWeek.Sunday);
        var daylightEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            11,
            1,
            DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1),
            new DateTime(2030, 12, 31),
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd);
        return TimeZoneInfo.CreateCustomTimeZone(
            "StandaloneTestEastern",
            TimeSpan.FromHours(-5),
            "Standalone Test Eastern",
            "Standalone Test Eastern",
            "Standalone Test Eastern Daylight",
            [rule]);
    }

    /// <summary>Verifies once schedules have no future boundary after their anchor.</summary>
    [Fact]
    public void OnceHasNoFutureBoundary()
    {
        DateTimeOffset anchor = DateTimeOffset.UtcNow;
        StandaloneScheduleCursor cursor = new(new StandaloneSchedule(StandaloneScheduleKind.Once, null, null), anchor, TimeZoneInfo.Utc);
        Assert.Null(cursor.GetNextDueAfter(anchor));
    }
}
