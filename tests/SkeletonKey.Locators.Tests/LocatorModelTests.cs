namespace SkeletonKey.Locators.Tests;

/// <summary>
/// Covers immutable locator domain contracts.
/// </summary>
public sealed class LocatorModelTests
{
    /// <summary>
    /// Verifies locator documents defensively copy dictionaries.
    /// </summary>
    [Fact]
    public void LocatorDocumentDefensivelyCopiesLocatorDictionary()
    {
        Dictionary<string, LocatorDefinition> locators = new()
        {
            ["button"] = new LocatorDefinition(strategies: [new LocatorStrategy("test-id", value: "save")]),
        };

        LocatorDocument document = new(id: "catalog", locators: locators);
        locators["other"] = new LocatorDefinition(strategies: [new LocatorStrategy("css", selector: ".other")]);

        Assert.Single(document.Locators);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, LocatorDefinition>)document.Locators).Add("x", locators["other"]));
    }

    /// <summary>
    /// Verifies locator definitions preserve strategy order and expose immutable strategy lists.
    /// </summary>
    [Fact]
    public void LocatorDefinitionPreservesStrategyOrderAndImmutability()
    {
        LocatorStrategy first = new("role", role: "button");
        LocatorStrategy second = new("test-id", value: "save");
        List<LocatorStrategy> strategies = [first, second];

        LocatorDefinition definition = new(within: "form", strategies: strategies);
        strategies.Reverse();

        Assert.Equal("form", definition.Within);
        Assert.Equal([first, second], definition.Strategies);
        Assert.Throws<NotSupportedException>(() => ((IList<LocatorStrategy>)definition.Strategies).Add(new LocatorStrategy("css", selector: ".x")));
    }

    /// <summary>
    /// Verifies locator cardinality values remain distinct.
    /// </summary>
    [Fact]
    public void LocatorCardinalityValuesRemainDistinct()
    {
        Assert.NotEqual(LocatorCardinality.One, LocatorCardinality.Many);
        Assert.NotEqual(LocatorCardinality.ZeroOrOne, LocatorCardinality.OneOrMore);
    }

    /// <summary>
    /// Verifies strategy contracts preserve match modes and selector text.
    /// </summary>
    [Fact]
    public void StrategiesPreserveMatchingAndSelectorText()
    {
        LocatorStrategy exact = new("text", value: "Save", match: LocatorTextMatchMode.Exact);
        LocatorStrategy contains = new("text", value: "Save", match: LocatorTextMatchMode.Contains);
        LocatorStrategy css = new("css", selector: "button.save");
        LocatorStrategy xpath = new("xpath", selector: "//button");

        Assert.Equal(LocatorTextMatchMode.Exact, exact.Match);
        Assert.Equal(LocatorTextMatchMode.Contains, contains.Match);
        Assert.Equal("button.save", css.Selector);
        Assert.Equal("//button", xpath.Selector);
    }

    /// <summary>
    /// Verifies locator references preserve exact versions.
    /// </summary>
    [Fact]
    public void LocatorReferencePreservesExactVersion()
    {
        LocatorReference reference = new("catalog", "save-button", "1.0.0");

        Assert.Equal("1.0.0", reference.Version);
    }
}
