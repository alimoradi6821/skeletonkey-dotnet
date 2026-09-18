using SkeletonKey.Locators;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Abstractions.Tests;

/// <summary>Covers Phase 3 provider-neutral structured web contracts.</summary>
public sealed class WebStructuredContractTests
{
    /// <summary>Verifies collection result and field dictionaries defensively own their input.</summary>
    [Fact]
    public void CollectionResultDefensivelyOwnsValues()
    {
        Dictionary<string, string?> source = new(StringComparer.Ordinal) { ["name"] = "Ada" };
        WebCollectionItem item = new(source);
        WebCollectionExtractionResult result = new([item], 1, false);

        source["name"] = "Grace";

        Assert.Equal("Ada", result.Items[0].Fields["name"]);
        Assert.Equal(1, result.TotalMatchedCount);
        Assert.False(result.Truncated);
    }

    /// <summary>Verifies attribute field declarations require an explicit attribute name.</summary>
    [Fact]
    public void AttributeFieldRequiresName()
    {
        ResolvedLocatorPlan locator = new("catalog", "0.1.0", "field", null, LocatorCardinality.One, [new ResolvedLocatorStrategy("css", selector: "a")]);

        Assert.Throws<ArgumentException>(() => new WebCollectionFieldDefinition("href", locator, WebCollectionFieldReadMode.Attribute));
    }

    /// <summary>Verifies scroll and wait contracts expose deterministic defaults.</summary>
    [Fact]
    public void StructuredRequestsExposeBoundedDefaults()
    {
        WebCollectionExtractionRequest extract = new(new WebTargetContext());
        WebWaitForConditionRequest wait = new(WebWaitConditionKind.Exists);
        WebScrollRequest scroll = new(DeltaY: 100);

        Assert.Equal(100, extract.MaximumItems);
        Assert.Equal(262144, extract.MaximumOutputCharacters);
        Assert.Equal(50, wait.PollIntervalMilliseconds);
        Assert.Equal(30000, wait.TimeoutMilliseconds);
        Assert.Equal(100, scroll.DeltaY);
    }
}
