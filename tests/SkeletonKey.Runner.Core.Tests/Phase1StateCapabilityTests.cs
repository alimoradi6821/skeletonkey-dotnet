using System.Text.Json.Nodes;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.State.Abstractions;
using SkeletonKey.State.BuiltIns;
using SkeletonKey.State.Sqlite;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Confirms the Phase 1 durable cross-execution state capability.</summary>
public sealed class Phase1StateCapabilityTests
{
    /// <summary>Verifies state survives independent provider instances and therefore process-style restarts.</summary>
    [Fact]
    public async Task SqliteStatePersistsAcrossProviderInstances()
    {
        string path = DatabasePath();
        WorkflowStateAddress address = new(WorkflowStateScopeKind.Workflow, "workflow-a", "item/1");
        try
        {
            string version;
            using (SqliteWorkflowStateStore first = new(path))
            {
                WorkflowStateEntry written = await first.PutAsync(address, new JsonObject { ["value"] = 42 });
                version = written.Version;
            }

            using SqliteWorkflowStateStore second = new(path);
            WorkflowStateEntry? loaded = await second.GetAsync(address);
            Assert.NotNull(loaded);
            Assert.Equal(version, loaded!.Version);
            Assert.Equal(42, loaded.Value!["value"]!.GetValue<int>());
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    /// <summary>Verifies concurrent create-if-absent compare/exchange has at most one winner.</summary>
    [Fact]
    public async Task SqliteCompareExchangeAllowsOnlyOneAbsentKeyWinner()
    {
        string path = DatabasePath();
        WorkflowStateAddress address = new(WorkflowStateScopeKind.Workflow, "workflow-a", "claim/1");
        try
        {
            using SqliteWorkflowStateStore store = new(path, busyTimeoutSeconds: 10);
            Task<WorkflowStateCompareExchangeResult>[] attempts =
            [
                store.CompareExchangeAsync(address, null, JsonValue.Create("a")).AsTask(),
                store.CompareExchangeAsync(address, null, JsonValue.Create("b")).AsTask(),
            ];
            WorkflowStateCompareExchangeResult[] results = await Task.WhenAll(attempts);
            Assert.Single(results.Where(static result => result.Succeeded));
            Assert.Single(results.Where(static result => !result.Succeeded));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    /// <summary>Verifies deleting and recreating a key never reuses the stale version token.</summary>
    [Fact]
    public async Task RecreatedStateReceivesNewVersion()
    {
        string path = DatabasePath();
        WorkflowStateAddress address = new(WorkflowStateScopeKind.Custom, "namespace", "key");
        try
        {
            using SqliteWorkflowStateStore store = new(path);
            WorkflowStateEntry first = await store.PutAsync(address, JsonValue.Create(1));
            Assert.True(await store.DeleteAsync(address));
            WorkflowStateEntry second = await store.PutAsync(address, JsonValue.Create(2));
            Assert.NotEqual(first.Version, second.Version);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    /// <summary>Verifies workflow-scoped handlers derive namespace from workflow identity across independent executions.</summary>
    [Fact]
    public async Task StateHandlersShareWorkflowScopeAcrossExecutions()
    {
        string path = DatabasePath();
        try
        {
            using SqliteWorkflowStateStore store = new(path);
            StatePutHandler put = new(store);
            StateGetHandler get = new(store);
            NodeExecutionIdentity firstIdentity = Identity("execution-a", "state.put");
            NodeExecutionIdentity secondIdentity = Identity("execution-b", "state.get");
            NodeHandlerResult written = await put.ExecuteAsync(
                new NodeExecutionRequest(firstIdentity, new JsonObject { ["key"] = "message/1", ["value"] = new JsonObject { ["done"] = true } }),
                new StubExecutionContext(firstIdentity));
            NodeHandlerResult loaded = await get.ExecuteAsync(
                new NodeExecutionRequest(secondIdentity, new JsonObject { ["key"] = "message/1" }),
                new StubExecutionContext(secondIdentity));

            Assert.Equal(NodeHandlerCompletionStatus.Succeeded, written.Status);
            Assert.Equal(NodeHandlerCompletionStatus.Succeeded, loaded.Status);
            Assert.True(loaded.Outputs.DataOutputs["found"].Values[0]!.GetValue<bool>());
            Assert.True(loaded.Outputs.DataOutputs["value"].Values[0]!["done"]!.GetValue<bool>());
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    /// <summary>Verifies the durable state catalog reserves the five generic operations.</summary>
    [Fact]
    public void StateCatalogContainsFiveGenericOperations()
    {
        string[] types = StateBuiltInWorkflowNodeCatalog.Catalog.Definitions.Select(static definition => definition.Type).OrderBy(static type => type, StringComparer.Ordinal).ToArray();
        Assert.Equal(["state.compareExchange", "state.delete", "state.exists", "state.get", "state.put"], types);
    }

    private static NodeExecutionIdentity Identity(string executionId, string type)
    {
        return new NodeExecutionIdentity(executionId, "invocation", null, "workflow-a", "node", new WorkflowNodeDefinitionKey(type, 1), "plan", "step", 1);
    }

    private static string DatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), "skeletonkey-phase1-state", Guid.NewGuid().ToString("N"), "state.db");
    }

    private static void DeleteDatabase(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class StubExecutionContext(NodeExecutionIdentity identity) : INodeExecutionContext
    {
        public NodeExecutionIdentity Identity { get; } = identity;

        public INodeExecutionEventWriter Events => null!;

        public INodeResourceAccessor Resources => null!;

        public SkeletonKey.Locators.INodeLocatorAccessor Locators => null!;
    }
}
