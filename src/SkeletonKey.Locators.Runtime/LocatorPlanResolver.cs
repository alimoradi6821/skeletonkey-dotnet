using SkeletonKey.Locators;

namespace SkeletonKey.Locators.Runtime;

/// <summary>
/// Resolves locator references into immutable provider-neutral locator plans without contacting a browser.
/// </summary>
public sealed class LocatorPlanResolver : ILocatorPlanResolver
{
    private readonly ILocatorDocumentRepository _repository;

    /// <summary>
    /// Initializes a locator plan resolver.
    /// </summary>
    /// <param name="repository">The explicit locator document repository.</param>
    public LocatorPlanResolver(ILocatorDocumentRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async ValueTask<ResolvedLocatorPlan> ResolveAsync(LocatorReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        string version = reference.Version ?? LocatorSpecification.CurrentVersion;
        LocatorDocumentLookupResult lookup = await _repository.GetAsync(reference.Catalog, version, cancellationToken).ConfigureAwait(false);
        if (!lookup.Found || lookup.Document is null)
        {
            throw new LocatorPlanResolutionException(LocatorPlanResolutionCodes.DocumentNotFound, "The exact locator document was not found.");
        }

        LocatorDocument document = lookup.Document;
        if (!string.Equals(document.SpecVersion, LocatorSpecification.CurrentVersion, StringComparison.Ordinal))
        {
            throw new LocatorPlanResolutionException(LocatorPlanResolutionCodes.UnsupportedVersion, "The locator document version is unsupported.");
        }

        List<ResolvedLocatorScope> scopes = [];
        HashSet<string> visiting = new(StringComparer.Ordinal);
        LocatorDefinition locator = ResolveDefinition(document, reference.Id, visiting, scopes);
        return new ResolvedLocatorPlan(
            document.Id,
            document.SpecVersion,
            reference.Id,
            locator.Description,
            locator.Cardinality,
            ConvertStrategies(locator.Strategies),
            scopes,
            $"{document.Id}@{document.SpecVersion}#{reference.Id}");
    }

    private static LocatorDefinition ResolveDefinition(LocatorDocument document, string locatorId, HashSet<string> visiting, List<ResolvedLocatorScope> scopes)
    {
        if (!document.Locators.TryGetValue(locatorId, out LocatorDefinition? locator))
        {
            throw new LocatorPlanResolutionException(LocatorPlanResolutionCodes.LocatorNotFound, "The locator ID was not found.");
        }

        if (!visiting.Add(locatorId))
        {
            throw new LocatorPlanResolutionException(LocatorPlanResolutionCodes.ScopeCycle, "The locator scope chain contains a cycle.");
        }

        if (locator.Within is not null)
        {
            LocatorDefinition parent = ResolveDefinition(document, locator.Within, visiting, scopes);
            scopes.Add(new ResolvedLocatorScope(locator.Within, parent.Cardinality, ConvertStrategies(parent.Strategies), parent.Description));
        }

        visiting.Remove(locatorId);
        return locator;
    }

    private static IReadOnlyList<ResolvedLocatorStrategy> ConvertStrategies(IReadOnlyList<LocatorStrategy> strategies)
    {
        return strategies.Select(static strategy => new ResolvedLocatorStrategy(strategy.Kind, strategy.Role, strategy.Name, strategy.Value, strategy.Selector, strategy.Match, strategy.CaseSensitive)).ToArray();
    }
}
