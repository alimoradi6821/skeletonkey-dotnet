using System.Collections.ObjectModel;

namespace SkeletonKey.Locators;

/// <summary>
/// Captures deterministic locator resolution diagnostics without browser objects.
/// </summary>
public sealed class LocatorResolutionTrace
{
    private readonly IReadOnlyList<LocatorStrategyAttempt> _attempts;

    /// <summary>
    /// Initializes a locator resolution trace.
    /// </summary>
    /// <param name="attempts">Ordered strategy attempts.</param>
    public LocatorResolutionTrace(IReadOnlyList<LocatorStrategyAttempt>? attempts = null)
    {
        _attempts = attempts is null ? Array.AsReadOnly(Array.Empty<LocatorStrategyAttempt>()) : new ReadOnlyCollection<LocatorStrategyAttempt>([.. attempts]);
    }

    /// <summary>Gets ordered strategy attempts.</summary>
    public IReadOnlyList<LocatorStrategyAttempt> Attempts => new ReadOnlyCollection<LocatorStrategyAttempt>([.. _attempts]);
}
