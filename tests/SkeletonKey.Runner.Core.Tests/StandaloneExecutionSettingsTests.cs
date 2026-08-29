using SkeletonKey.Runner.Core;

namespace SkeletonKey.Runner.Core.Tests;

public sealed class StandaloneExecutionSettingsTests
{
    [Fact]
    public void ParseAcceptsOnceSchedule()
    {
        StandaloneExecutionSettings settings = StandaloneExecutionSettings.Parse("""
            {
              "specVersion": "0.1",
              "schedule": { "type": "once" }
            }
            """);

        Assert.Equal(StandaloneScheduleKind.Once, settings.Schedule.Kind);
        Assert.False(settings.Execution.RunImmediately);
        Assert.Equal(StandaloneOverlapPolicy.Skip, settings.Execution.Overlap);
        Assert.True(settings.Execution.ContinueAfterFailure);
    }

    [Fact]
    public void ParseAcceptsIntervalAndExecutionPolicy()
    {
        StandaloneExecutionSettings settings = StandaloneExecutionSettings.Parse("""
            {
              "specVersion": "0.1",
              "schedule": { "type": "interval", "interval": "PT5M" },
              "execution": {
                "runImmediately": true,
                "overlap": "skip",
                "continueAfterFailure": false
              }
            }
            """);

        Assert.Equal(StandaloneScheduleKind.Interval, settings.Schedule.Kind);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.Schedule.Interval);
        Assert.True(settings.Execution.RunImmediately);
        Assert.False(settings.Execution.ContinueAfterFailure);
    }

    [Fact]
    public void ParseRejectsUnknownProperties()
    {
        StandaloneSettingsException error = Assert.Throws<StandaloneSettingsException>(() => StandaloneExecutionSettings.Parse("""
            {
              "specVersion": "0.1",
              "schedule": { "type": "once", "cron": "* * * * *" }
            }
            """));

        Assert.Equal("SKX1008", error.Code);
    }

    [Fact]
    public void ParseRejectsDuplicateProperties()
    {
        StandaloneSettingsException error = Assert.Throws<StandaloneSettingsException>(() => StandaloneExecutionSettings.Parse("""
            {
              "specVersion": "0.1",
              "specVersion": "0.1",
              "schedule": { "type": "once" }
            }
            """));

        Assert.Equal("SKX1009", error.Code);
    }

    [Theory]
    [InlineData("PT0S")]
    [InlineData("PT0.5S")]
    [InlineData("P366D")]
    public void ParseRejectsOutOfRangeIntervals(string interval)
    {
        StandaloneSettingsException error = Assert.Throws<StandaloneSettingsException>(() => StandaloneExecutionSettings.Parse($$"""
            {
              "specVersion": "0.1",
              "schedule": { "type": "interval", "interval": "{{interval}}" }
            }
            """));

        Assert.Equal("SKX1017", error.Code);
    }

    [Fact]
    public void ParseRejectsCalendarMonthIntervals()
    {
        StandaloneSettingsException error = Assert.Throws<StandaloneSettingsException>(() => StandaloneExecutionSettings.Parse("""
            {
              "specVersion": "0.1",
              "schedule": { "type": "interval", "interval": "P1M" }
            }
            """));

        Assert.Equal("SKX1025", error.Code);
    }

    [Fact]
    public void ParseRejectsNonSkipOverlap()
    {
        StandaloneSettingsException error = Assert.Throws<StandaloneSettingsException>(() => StandaloneExecutionSettings.Parse("""
            {
              "specVersion": "0.1",
              "schedule": { "type": "interval", "interval": "PT5M" },
              "execution": { "overlap": "parallel" }
            }
            """));

        Assert.Equal("SKX1024", error.Code);
    }
}
