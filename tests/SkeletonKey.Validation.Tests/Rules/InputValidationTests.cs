using System.Text.Json.Nodes;
using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Inputs;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers workflow input declaration validation.
/// </summary>
public sealed class InputValidationTests
{
    /// <summary>
    /// Verifies that valid input names are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidInputNames()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["username"] = new(WorkflowInputType.String),
            ["_user"] = new(WorkflowInputType.String),
            ["retry-count"] = new(WorkflowInputType.Integer),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidInputName);
    }

    /// <summary>
    /// Verifies that invalid input names are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidInputNames()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["user/name"] = new(WorkflowInputType.String),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidInputName && issue.Path == "/inputs/user~1name");
    }

    /// <summary>
    /// Verifies that required inputs cannot declare non-null defaults.
    /// </summary>
    [Fact]
    public void RejectsRequiredInputWithNonNullDefault()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["username"] = new(WorkflowInputType.String, required: true, defaultValue: JsonValue.Create("Ada")),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.RequiredInputDeclaresDefault && issue.Path == "/inputs/username/default");
    }

    /// <summary>
    /// Verifies that required inputs cannot declare explicit null defaults.
    /// </summary>
    [Fact]
    public void RejectsRequiredInputWithExplicitNullDefault()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["username"] = new(WorkflowInputType.String, required: true, hasDefault: true),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.RequiredInputDeclaresDefault && issue.Path == "/inputs/username/default");
    }

    /// <summary>
    /// Verifies that optional inputs can declare explicit null defaults.
    /// </summary>
    [Fact]
    public void AllowsOptionalInputWithExplicitNullDefault()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["username"] = new(WorkflowInputType.String, hasDefault: true),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that matching default types are accepted.
    /// </summary>
    [Fact]
    public void AcceptsMatchingDefaults()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["text"] = new(WorkflowInputType.String, defaultValue: JsonValue.Create("hello")),
            ["integer"] = new(WorkflowInputType.Integer, defaultValue: JsonValue.Create(12)),
            ["number"] = new(WorkflowInputType.Number, defaultValue: JsonValue.Create(1.25)),
            ["boolean"] = new(WorkflowInputType.Boolean, defaultValue: JsonValue.Create(true)),
            ["object"] = new(WorkflowInputType.Object, defaultValue: new JsonObject { ["x"] = 1 }),
            ["array"] = new(WorkflowInputType.Array, defaultValue: new JsonArray(1, 2)),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.DoesNotContain(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InputDefaultTypeMismatch);
    }

    /// <summary>
    /// Verifies that fractional integer defaults are rejected.
    /// </summary>
    [Fact]
    public void RejectsFractionalIntegerDefault()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["count"] = new(WorkflowInputType.Integer, defaultValue: JsonValue.Create(1.5)),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InputDefaultTypeMismatch);
    }

    /// <summary>
    /// Verifies that mismatched default types are rejected.
    /// </summary>
    [Fact]
    public void RejectsMismatchedDefaultType()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["count"] = new(WorkflowInputType.Number, defaultValue: JsonValue.Create(true)),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InputDefaultTypeMismatch && issue.Path == "/inputs/count/default");
    }

    /// <summary>
    /// Verifies that non-finite programmatic numeric defaults are rejected.
    /// </summary>
    [Fact]
    public void RejectsNonFiniteProgrammaticNumberDefault()
    {
        Dictionary<string, WorkflowInputDefinition> inputs = new()
        {
            ["value"] = new(WorkflowInputType.Number, defaultValue: JsonValue.Create(double.NaN)),
        };

        WorkflowValidationResult result = ValidationTestData.Validate(ValidationTestData.CreateValidWorkflow(inputs: inputs));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InputDefaultTypeMismatch);
    }
}
