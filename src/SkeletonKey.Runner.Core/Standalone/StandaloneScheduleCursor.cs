namespace SkeletonKey.Runner.Core;

/// <summary>Calculates future due boundaries for one validated standalone schedule.</summary>
public sealed class StandaloneScheduleCursor
{
    private readonly StandaloneSchedule _schedule;
    private readonly DateTimeOffset _anchorUtc;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>Creates a cursor anchored at application initialization.</summary>
    public StandaloneScheduleCursor(StandaloneSchedule schedule, DateTimeOffset anchorUtc, TimeZoneInfo? timeZone = null)
    {
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _anchorUtc = anchorUtc.ToUniversalTime();
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    /// <summary>Returns the first scheduled occurrence strictly after the supplied instant.</summary>
    public DateTimeOffset? GetNextDueAfter(DateTimeOffset afterUtc)
    {
        DateTimeOffset after = afterUtc.ToUniversalTime();
        return _schedule.Kind switch
        {
            StandaloneScheduleKind.Once => null,
            StandaloneScheduleKind.Interval => NextInterval(after),
            StandaloneScheduleKind.Daily => NextDaily(after),
            _ => throw new InvalidOperationException("Unknown standalone schedule kind."),
        };
    }

    private DateTimeOffset NextInterval(DateTimeOffset after)
    {
        TimeSpan interval = _schedule.Interval ?? throw new InvalidOperationException("Interval schedule is missing its interval.");
        if (after < _anchorUtc)
        {
            return _anchorUtc;
        }

        long elapsedTicks = (after - _anchorUtc).Ticks;
        long completedIntervals = elapsedTicks / interval.Ticks;
        checked
        {
            return _anchorUtc.AddTicks((completedIntervals + 1) * interval.Ticks);
        }
    }

    private DateTimeOffset NextDaily(DateTimeOffset afterUtc)
    {
        TimeOnly dailyTime = _schedule.DailyTime ?? throw new InvalidOperationException("Daily schedule is missing its wall-clock time.");
        DateTime localAfter = TimeZoneInfo.ConvertTime(afterUtc, _timeZone).DateTime;
        DateOnly candidateDate = DateOnly.FromDateTime(localAfter);

        for (int dayOffset = 0; dayOffset <= 370; dayOffset++)
        {
            DateTime localCandidate = DateTime.SpecifyKind(candidateDate.AddDays(dayOffset).ToDateTime(dailyTime), DateTimeKind.Unspecified);
            localCandidate = NormalizeInvalidLocalTime(localCandidate);
            DateTimeOffset candidate = ResolveLocalCandidate(localCandidate).ToUniversalTime();
            if (candidate > afterUtc)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not calculate the next daily standalone occurrence.");
    }

    private DateTime NormalizeInvalidLocalTime(DateTime candidate)
    {
        // Spring-forward gaps are normalized to the first valid local minute on the same date.
        for (int minute = 0; minute <= 180 && _timeZone.IsInvalidTime(candidate); minute++)
        {
            candidate = candidate.AddMinutes(1);
        }

        if (_timeZone.IsInvalidTime(candidate))
        {
            throw new InvalidOperationException("Could not normalize an invalid local daily schedule time.");
        }

        return candidate;
    }

    private DateTimeOffset ResolveLocalCandidate(DateTime candidate)
    {
        if (_timeZone.IsAmbiguousTime(candidate))
        {
            // Choose the larger UTC offset, which represents the earlier of the two UTC instants.
            TimeSpan offset = _timeZone.GetAmbiguousTimeOffsets(candidate).Max();
            return new DateTimeOffset(candidate, offset);
        }

        return new DateTimeOffset(candidate, _timeZone.GetUtcOffset(candidate));
    }
}
