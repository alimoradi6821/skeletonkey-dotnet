using SkeletonKey.Locators.Validation;

namespace SkeletonKey.Locators.Validation.Tests;

/// <summary>
/// Covers locator semantic validation.
/// </summary>
public sealed class LocatorSemanticValidatorTests
{
    private readonly LocatorSemanticValidator _validator = new();

    /// <summary>
    /// Verifies valid semantic locators, fallback strategies, and within scope are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidSemanticLocatorsAndScopes()
    {
        LocatorDocument document = new(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["form"] = new LocatorDefinition(strategies: [new LocatorStrategy("role", role: "form", name: "Contact")]),
            ["save"] = new LocatorDefinition(within: "form", strategies:
            [
                new LocatorStrategy("role", role: "button", name: "Save"),
                new LocatorStrategy("test-id", value: "save-contact"),
                new LocatorStrategy("css", selector: "button.save"),
                new LocatorStrategy("xpath", selector: "//button"),
            ]),
        });

        Assert.True(_validator.Validate(document).IsValid);
    }

    /// <summary>
    /// Verifies invalid document and locator IDs are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidIds()
    {
        LocatorDocument document = new(id: "bad.id", locators: new Dictionary<string, LocatorDefinition>
        {
            ["bad id"] = new LocatorDefinition(strategies: [new LocatorStrategy("test-id", value: "x")]),
        });

        IReadOnlyList<string> codes = [.. _validator.Validate(document).Issues.Select(static issue => issue.Code)];

        Assert.Contains(LocatorValidationCodes.InvalidLocatorDocumentId, codes);
        Assert.Contains(LocatorValidationCodes.InvalidLocatorId, codes);
    }

    /// <summary>
    /// Verifies missing and duplicate strategies are rejected.
    /// </summary>
    [Fact]
    public void RejectsMissingAndDuplicateStrategies()
    {
        LocatorDocument document = new(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["empty"] = new LocatorDefinition(),
            ["duplicate"] = new LocatorDefinition(strategies: [new LocatorStrategy("test-id", value: "x"), new LocatorStrategy("test-id", value: "x")]),
        });

        IReadOnlyList<string> codes = [.. _validator.Validate(document).Issues.Select(static issue => issue.Code)];

        Assert.Contains(LocatorValidationCodes.MissingLocatorStrategies, codes);
        Assert.Contains(LocatorValidationCodes.DuplicateEquivalentStrategy, codes);
    }

    /// <summary>
    /// Verifies unknown, direct self, and indirect scope cycles are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidScopesAndCycles()
    {
        LocatorDocument document = new(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["unknown"] = new LocatorDefinition(within: "missing", strategies: [new LocatorStrategy("test-id", value: "x")]),
            ["self"] = new LocatorDefinition(within: "self", strategies: [new LocatorStrategy("test-id", value: "x")]),
            ["a"] = new LocatorDefinition(within: "b", strategies: [new LocatorStrategy("test-id", value: "a")]),
            ["b"] = new LocatorDefinition(within: "a", strategies: [new LocatorStrategy("test-id", value: "b")]),
        });

        IReadOnlyList<string> codes = [.. _validator.Validate(document).Issues.Select(static issue => issue.Code)];

        Assert.Contains(LocatorValidationCodes.InvalidScopedLocatorReference, codes);
        Assert.Contains(LocatorValidationCodes.LocatorDirectlyScopesItself, codes);
        Assert.Contains(LocatorValidationCodes.LocatorScopeCycle, codes);
    }

    /// <summary>
    /// Verifies empty semantic text and selectors are rejected.
    /// </summary>
    [Fact]
    public void RejectsEmptyStrategyValuesAndSelectors()
    {
        LocatorDocument document = new(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["semantic"] = new LocatorDefinition(strategies: [new LocatorStrategy("text", value: " ")]),
            ["css"] = new LocatorDefinition(strategies: [new LocatorStrategy("css", selector: "")]),
            ["xpath"] = new LocatorDefinition(strategies: [new LocatorStrategy("xpath", selector: "")]),
        });

        IReadOnlyList<string> codes = [.. _validator.Validate(document).Issues.Select(static issue => issue.Code)];

        Assert.Contains(LocatorValidationCodes.InvalidStrategyValue, codes);
        Assert.Contains(LocatorValidationCodes.InvalidCssSelectorText, codes);
        Assert.Contains(LocatorValidationCodes.InvalidXPathSelectorText, codes);
    }

    /// <summary>
    /// Verifies exact version syntax and deterministic non-mutating validation.
    /// </summary>
    [Fact]
    public void ProducesDeterministicDiagnosticsAndDoesNotMutate()
    {
        LocatorReference reference = new("catalog", "save", "latest");
        LocatorDocument document = new(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["save"] = new LocatorDefinition(strategies: [new LocatorStrategy("test-id", value: "")]),
        });
        IReadOnlyList<string> before = [.. document.Locators.Keys];

        string[] first = [.. _validator.Validate(document).Issues.Select(static issue => $"{issue.Code}:{issue.Path}")];
        string[] second = [.. _validator.Validate(document).Issues.Select(static issue => $"{issue.Code}:{issue.Path}")];
        LocatorValidationResult referenceResult = _validator.ValidateReference(reference);

        Assert.Equal(first, second);
        Assert.Equal(before, [.. document.Locators.Keys]);
        Assert.Contains(referenceResult.Issues, static issue => issue.Code == LocatorValidationCodes.InvalidExactLocatorVersion);
    }
}
