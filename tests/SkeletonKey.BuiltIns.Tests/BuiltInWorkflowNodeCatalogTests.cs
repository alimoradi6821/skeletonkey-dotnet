using SkeletonKey.Catalog;
using SkeletonKey.Catalog.Validation;

namespace SkeletonKey.BuiltIns.Tests;

/// <summary>
/// Covers consistency of contract-only built-in node definitions.
/// </summary>
public sealed class BuiltInWorkflowNodeCatalogTests
{
    private static readonly string[] _reservedTypes =
    [
        "core.start",
        "core.end",
        "core.return",
        "workflow.invoke",
        "flow.if",
        "flow.switch",
        "flow.foreach",
        "flow.repeat",
        "flow.while",
        "interaction.request",
    ];

    /// <summary>
    /// Verifies exactly one definition exists for every reserved built-in node contract.
    /// </summary>
    [Fact]
    public void DefinesExactlyOneDefinitionForEveryReservedNodeType()
    {
        Assert.Equal(_reservedTypes.Order(StringComparer.Ordinal), BuiltInWorkflowNodeCatalog.Document.Definitions.Select(static definition => definition.Type).Order(StringComparer.Ordinal));
        foreach (string type in _reservedTypes)
        {
            Assert.Single(BuiltInWorkflowNodeCatalog.Document.Definitions, definition => definition.Type == type && definition.Version == 1);
        }
    }

    /// <summary>
    /// Verifies built-in definitions pass catalog semantic validation.
    /// </summary>
    [Fact]
    public void BuiltInCatalogPassesSemanticValidation()
    {
        NodeCatalogValidationResult result = new NodeCatalogSemanticValidator().Validate(BuiltInWorkflowNodeCatalog.Document);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies reserved built-in ports match semantic validation contracts.
    /// </summary>
    [Fact]
    public void BuiltInPortsMatchReservedContracts()
    {
        WorkflowNodeDefinition switchNode = Definition("flow.switch");
        WorkflowNodeDefinition invoke = Definition("workflow.invoke");
        WorkflowNodeDefinition returnNode = Definition("core.return");
        WorkflowNodeDefinition interaction = Definition("interaction.request");

        Assert.Equal(["default"], switchNode.Outputs.Keys);
        Assert.Single(switchNode.DynamicPorts);
        Assert.Equal("/cases", switchNode.DynamicPorts[0].SourcePointer);
        Assert.Equal("/id", switchNode.DynamicPorts[0].IdPointer);
        Assert.Equal(["result"], invoke.Outputs.Keys);
        Assert.True(returnNode.Behavior.Terminal);
        Assert.Equal(["result"], interaction.Outputs.Keys);
        Assert.True(interaction.Behavior.MaySuspend);
    }

    /// <summary>
    /// Verifies reserved control-port conventions remain stable for built-in catalogs.
    /// </summary>
    [Fact]
    public void BuiltInControlPortsFollowConventions()
    {
        Assert.Equal(["main"], Definition("core.start").Outputs.Keys);
        Assert.Equal(["main"], Definition("core.end").Inputs.Keys);
        Assert.Equal(["main"], Definition("core.return").Inputs.Keys);
        Assert.Equal(["main"], Definition("workflow.invoke").Inputs.Keys);
        Assert.Equal(["result"], Definition("workflow.invoke").Outputs.Keys);
        Assert.Equal(["main"], Definition("flow.if").Inputs.Keys);
        Assert.Equal(["false", "true"], Definition("flow.if").Outputs.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["main"], Definition("flow.switch").Inputs.Keys);
        Assert.Equal(["default"], Definition("flow.switch").Outputs.Keys);

        foreach (string type in new[] { "flow.foreach", "flow.repeat", "flow.while" })
        {
            WorkflowNodeDefinition loop = Definition(type);
            Assert.Equal(["break", "continue", "main"], loop.Inputs.Keys.Order(StringComparer.Ordinal));
            Assert.Equal(["body", "completed"], loop.Outputs.Keys.Order(StringComparer.Ordinal));
        }

        foreach (WorkflowNodeDefinition definition in BuiltInWorkflowNodeCatalog.Document.Definitions)
        {
            Assert.Equal(definition.Inputs.Count, definition.Inputs.Keys.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(definition.Outputs.Count, definition.Outputs.Keys.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    /// Verifies built-in examples include every required parameter schema key.
    /// </summary>
    [Fact]
    public void BuiltInExamplesSatisfyRequiredParameterKeys()
    {
        foreach (WorkflowNodeDefinition definition in BuiltInWorkflowNodeCatalog.Document.Definitions)
        {
            string[] required = definition.ParametersSchema?["required"]?.AsArray().Select(static value => value!.GetValue<string>()).ToArray() ?? [];
            foreach (System.Text.Json.Nodes.JsonObject example in definition.ParameterExamples)
            {
                foreach (string key in required)
                {
                    Assert.True(example.ContainsKey(key), $"{definition.Type} example is missing {key}.");
                }
            }
        }
    }

    private static WorkflowNodeDefinition Definition(string type)
    {
        Assert.True(BuiltInWorkflowNodeCatalog.Catalog.TryGetDefinition(type, 1, out WorkflowNodeDefinition? definition));
        return definition!;
    }
}
