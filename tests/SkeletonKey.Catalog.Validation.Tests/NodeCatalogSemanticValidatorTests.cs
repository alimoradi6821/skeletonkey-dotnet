namespace SkeletonKey.Catalog.Validation.Tests;

/// <summary>
/// Covers node catalog semantic validation contracts.
/// </summary>
public sealed class NodeCatalogSemanticValidatorTests
{
    private readonly NodeCatalogSemanticValidator _validator = new();

    /// <summary>
    /// Verifies valid catalog metadata passes semantic validation.
    /// </summary>
    [Fact]
    public void ValidCatalogPasses()
    {
        NodeCatalogDocument document = new(
            id: "catalog",
            version: "1.0.0",
            definitions:
            [
                new(
                    "core.log",
                    1,
                    inputs: new Dictionary<string, WorkflowPortDefinition> { ["main"] = new("main", WorkflowPortDirection.Input) },
                    outputs: new Dictionary<string, WorkflowPortDefinition> { ["result"] = new("result", WorkflowPortDirection.Output) },
                    capabilities: ["logging.write"]),
            ]);

        NodeCatalogValidationResult result = _validator.Validate(document);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies duplicate exact definitions are reported deterministically.
    /// </summary>
    [Fact]
    public void DuplicateDefinitionProducesStableCode()
    {
        NodeCatalogDocument document = new(
            id: "catalog",
            version: "1.0.0",
            definitions: [new("core.log", 1), new("core.log", 1)]);

        NodeCatalogValidationResult result = _validator.Validate(document);

        Assert.Contains(result.Issues, issue => issue.Code == NodeCatalogValidationCodes.DuplicateNodeDefinition);
    }

    /// <summary>
    /// Verifies invalid dynamic switch rules are reported.
    /// </summary>
    [Fact]
    public void InvalidDynamicSwitchRuleProducesStableCode()
    {
        NodeCatalogDocument document = new(
            id: "catalog",
            version: "1.0.0",
            definitions:
            [
                new(
                    "flow.switch",
                    1,
                    dynamicPorts: [new(WorkflowDynamicPortRuleKind.SwitchCases, WorkflowPortDirection.Input, "/bad", "/id")]),
            ]);

        NodeCatalogValidationResult result = _validator.Validate(document);

        Assert.Contains(result.Issues, issue => issue.Code == NodeCatalogValidationCodes.InvalidDynamicPortRule);
    }
}
