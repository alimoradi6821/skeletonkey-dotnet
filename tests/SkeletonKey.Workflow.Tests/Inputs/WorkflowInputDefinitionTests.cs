using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Inputs;

namespace SkeletonKey.Workflow.Tests.Inputs;

/// <summary>
/// Covers workflow input definition behavior.
/// </summary>
public sealed class WorkflowInputDefinitionTests
{
    /// <summary>
    /// Verifies the declared input type is preserved.
    /// </summary>
    [Fact]
    public void PreservesDeclaredInputType()
    {
        WorkflowInputDefinition definition = new(WorkflowInputType.Number);

        Assert.Equal(WorkflowInputType.Number, definition.Type);
    }

    /// <summary>
    /// Verifies the required flag is preserved.
    /// </summary>
    [Fact]
    public void PreservesRequiredState()
    {
        WorkflowInputDefinition definition = new(WorkflowInputType.String, required: true);

        Assert.True(definition.Required);
    }

    /// <summary>
    /// Verifies default JSON values are cloned during construction.
    /// </summary>
    [Fact]
    public void DefensivelyClonesDefaultJsonValues()
    {
        JsonObject defaultValue = new()
        {
            ["name"] = "Ada",
        };

        WorkflowInputDefinition definition = new(WorkflowInputType.Object, defaultValue: defaultValue);

        defaultValue["name"] = "Grace";

        Assert.Equal("Ada", definition.Default!["name"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies returned default mutation does not affect the definition.
    /// </summary>
    [Fact]
    public void ExternalMutationOfReturnedDefault_DoesNotChangeDefinition()
    {
        WorkflowInputDefinition definition = new(
            WorkflowInputType.Object,
            defaultValue: new JsonObject
            {
                ["name"] = "Ada",
            });

        JsonNode returnedDefault = definition.Default!;
        returnedDefault["name"] = "Grace";

        Assert.Equal("Ada", definition.Default!["name"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies definitions may omit default values.
    /// </summary>
    [Fact]
    public void AllowsNoDefaultValue()
    {
        WorkflowInputDefinition definition = new(WorkflowInputType.Boolean);

        Assert.Null(definition.Default);
    }
    /// <summary>
    /// Verifies explicit JSON null defaults can be represented distinctly from omitted defaults.
    /// </summary>
    [Fact]
    public void PreservesExplicitNullDefaultState()
    {
        WorkflowInputDefinition definition = new(WorkflowInputType.String, hasDefault: true);

        Assert.True(definition.HasDefault);
        Assert.Null(definition.Default);
    }
}

