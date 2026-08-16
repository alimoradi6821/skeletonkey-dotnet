using System.Text.Json.Nodes;
using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Inputs;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers workflow variable declaration validation.
/// </summary>
public sealed class VariableValidationTests
{
    /// <summary>
    /// Verifies that valid variable names are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidVariableNames()
    {
        Dictionary<string, JsonNode?> variables = new()
        {
            ["username"] = "Ada",
            ["_user"] = true,
            ["retry-count"] = 3,
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(variables: variables));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidVariableName);
    }

    /// <summary>
    /// Verifies that invalid variable names are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidVariableNames()
    {
        Dictionary<string, JsonNode?> variables = new()
        {
            ["user name"] = "Ada",
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(variables: variables));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidVariableName);
    }

    /// <summary>
    /// Verifies that null variable values are allowed.
    /// </summary>
    [Fact]
    public void AllowsNullVariableValues()
    {
        Dictionary<string, JsonNode?> variables = new()
        {
            ["maybe"] = null,
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(variables: variables));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that identical input and variable names are allowed.
    /// </summary>
    [Fact]
    public void AllowsIdenticalInputAndVariableNames()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["username"] = new(WorkflowInputType.String),
        };
        Dictionary<string, JsonNode?> variables = new()
        {
            ["username"] = "Ada",
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs, variables: variables));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that variable names are escaped in JSON Pointer paths.
    /// </summary>
    [Fact]
    public void EscapesVariableNamesInJsonPointerPaths()
    {
        Dictionary<string, JsonNode?> variables = new()
        {
            ["bad/name"] = 1,
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(variables: variables));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidVariableName && issue.Path == "/variables/bad~1name");
    }
}
