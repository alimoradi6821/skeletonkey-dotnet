using SkeletonKey.Locators;

namespace SkeletonKey.Locators.Runtime.Tests;

/// <summary>
/// Covers provider-neutral locator runtime contracts.
/// </summary>
public sealed class LocatorRuntimeTests
{
    /// <summary>
    /// Verifies exact catalog and version lookup with no latest fallback.
    /// </summary>
    [Fact]
    public async Task RepositoryUsesExactCatalogAndVersion()
    {
        LocatorDocument document = Document("catalog", "0.1.0", ("button", new LocatorDefinition(strategies: [new("text", value: "Save")])));
        ImmutableLocatorDocumentRepository repository = new([document]);

        Assert.True((await repository.GetAsync("catalog", "0.1.0")).Found);
        Assert.False((await repository.GetAsync("Catalog", "0.1.0")).Found);
        Assert.False((await repository.GetAsync("catalog", "0.2.0")).Found);
    }

    /// <summary>
    /// Verifies duplicate exact locator catalog identities are rejected.
    /// </summary>
    [Fact]
    public void RepositoryRejectsDuplicateExactIdentities()
    {
        LocatorDocument first = Document("catalog", "0.1.0", ("a", new LocatorDefinition(strategies: [new("css", selector: "#a")])));
        LocatorDocument second = Document("catalog", "0.1.0", ("b", new LocatorDefinition(strategies: [new("css", selector: "#b")])));

        Assert.Throws<ArgumentException>(() => new ImmutableLocatorDocumentRepository([first, second]));
    }

    /// <summary>
    /// Verifies resolved plans preserve scope and strategy order.
    /// </summary>
    [Fact]
    public async Task ResolverPreservesWithinChainAndFallbackOrder()
    {
        LocatorDocument document = Document(
            "catalog",
            "0.1.0",
            ("panel", new LocatorDefinition(strategies: [new("css", selector: "#panel")])),
            ("button", new LocatorDefinition(within: "panel", strategies: [new("role", role: "button", name: "Save"), new("text", value: "Save")])));
        LocatorPlanResolver resolver = new(new ImmutableLocatorDocumentRepository([document]));

        ResolvedLocatorPlan plan = await resolver.ResolveAsync(new LocatorReference("catalog", "button", "0.1.0"));

        Assert.Equal("button", plan.LocatorId);
        Assert.Equal(["panel"], plan.Scopes.Select(static scope => scope.LocatorId));
        Assert.Equal(["role", "text"], plan.Strategies.Select(static strategy => strategy.Kind));
    }

    /// <summary>
    /// Verifies within cycles fail defensively before browser execution.
    /// </summary>
    [Fact]
    public async Task ResolverRejectsWithinCycles()
    {
        LocatorDocument document = Document(
            "catalog",
            "0.1.0",
            ("a", new LocatorDefinition(within: "b", strategies: [new("css", selector: "#a")])),
            ("b", new LocatorDefinition(within: "a", strategies: [new("css", selector: "#b")])));
        LocatorPlanResolver resolver = new(new ImmutableLocatorDocumentRepository([document]));

        LocatorPlanResolutionException exception = await Assert.ThrowsAsync<LocatorPlanResolutionException>(async () => await resolver.ResolveAsync(new LocatorReference("catalog", "a", "0.1.0")));
        Assert.Equal(LocatorPlanResolutionCodes.ScopeCycle, exception.Code);
    }

    private static LocatorDocument Document(string id, string version, params (string Id, LocatorDefinition Definition)[] locators)
    {
        return new LocatorDocument(specVersion: version, id: id, locators: locators.ToDictionary(static item => item.Id, static item => item.Definition, StringComparer.Ordinal));
    }
}
