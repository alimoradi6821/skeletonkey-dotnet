using System.Text.Json.Nodes;
using SkeletonKey.Workflow.ControlFlow;

namespace SkeletonKey.Workflow.Tests.ControlFlow;

/// <summary>
/// Covers immutable control-flow contract types.
/// </summary>
public sealed class WorkflowControlFlowContractTests
{
    /// <summary>
    /// Verifies foreach execution policy defaults to sequential.
    /// </summary>
    [Fact]
    public void ForEachExecutionPolicyDefaultsToSequential()
    {
        WorkflowForEachExecutionPolicy policy = new();

        Assert.Equal(WorkflowForEachExecutionMode.Sequential, policy.Mode);
        Assert.Null(policy.MaxConcurrency);
    }

    /// <summary>
    /// Verifies foreach execution policy preserves parallel concurrency values.
    /// </summary>
    [Fact]
    public void ForEachExecutionPolicyPreservesValues()
    {
        WorkflowForEachExecutionPolicy policy = new(WorkflowForEachExecutionMode.Parallel, 4);

        Assert.Equal(WorkflowForEachExecutionMode.Parallel, policy.Mode);
        Assert.Equal(4, policy.MaxConcurrency);
    }

    /// <summary>
    /// Verifies switch cases preserve declared values.
    /// </summary>
    [Fact]
    public void SwitchCasePreservesDeclaredValues()
    {
        JsonObject when = new()
        {
            ["$expression"] = "inputs.method == 'phone'",
        };

        WorkflowSwitchCase switchCase = new("phone", when, "Phone path");
        when["$expression"] = "changed";

        Assert.Equal("phone", switchCase.Id);
        Assert.Equal("Phone path", switchCase.Description);
        Assert.Equal("inputs.method == 'phone'", switchCase.When!["$expression"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies switch case workflow data is immutable from callers.
    /// </summary>
    [Fact]
    public void SwitchCaseWorkflowDataIsDefensivelyCloned()
    {
        WorkflowSwitchCase switchCase = new("phone", new JsonObject { ["$expression"] = "true" });

        switchCase.When!["$expression"] = "false";

        Assert.Equal("true", switchCase.When!["$expression"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies return outcome data remains workflow data.
    /// </summary>
    [Fact]
    public void ReturnOutcomeDataRemainsWorkflowData()
    {
        JsonObject data = new()
        {
            ["accountId"] = new JsonObject
            {
                ["$binding"] = new JsonObject
                {
                    ["source"] = "input",
                    ["name"] = "account",
                    ["path"] = "/accountId",
                },
            },
        };

        WorkflowReturnOutcome outcome = new("requires-action", "account.logged-out", JsonValue.Create("Login required"), data);
        data["accountId"] = "changed";

        Assert.Equal("requires-action", outcome.Kind);
        Assert.Equal("account.logged-out", outcome.Code);
        Assert.Equal("input", outcome.Data!["accountId"]!["$binding"]!["source"]!.GetValue<string>());
    }
}
