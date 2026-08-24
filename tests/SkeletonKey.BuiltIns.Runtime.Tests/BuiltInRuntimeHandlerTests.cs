using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Abstractions.Interaction;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Runtime;

namespace SkeletonKey.BuiltIns.Runtime.Tests;

/// <summary>
/// Covers executable built-in node handlers and immutable exact resolution.
/// </summary>
public sealed class BuiltInRuntimeHandlerTests
{
    /// <summary>
    /// Verifies the start handler activates the expected control port.
    /// </summary>
    [Fact]
    public async Task CoreStartActivatesExpectedPort()
    {
        CoreStartHandler handler = new();

        NodeHandlerResult result = await handler.ExecuteAsync(Request("core.start"), Context("core.start"));

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Equal(["main"], result.Outputs.ActivatedControlOutputs);
        Assert.Empty(result.Outputs.DataOutputs);
    }

    /// <summary>
    /// Verifies the end handler succeeds without producing outputs.
    /// </summary>
    [Fact]
    public async Task CoreEndCompletesWithoutOutputs()
    {
        CoreEndHandler handler = new();

        NodeHandlerResult result = await handler.ExecuteAsync(Request("core.end"), Context("core.end"));

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.Empty(result.Outputs.ActivatedControlOutputs);
        Assert.Empty(result.Outputs.DataOutputs);
        Assert.Null(result.Metadata);
    }

    /// <summary>
    /// Verifies the return handler produces terminal outcome metadata without control outputs.
    /// </summary>
    [Fact]
    public async Task CoreReturnProducesTerminalResultMetadata()
    {
        CoreReturnHandler handler = new();
        JsonObject parameters = new()
        {
            ["outcome"] = new JsonObject
            {
                ["kind"] = "success",
                ["code"] = "done",
            },
        };

        NodeHandlerResult result = await handler.ExecuteAsync(Request("core.return", parameters), Context("core.return"));

        Assert.Empty(result.Outputs.ActivatedControlOutputs);
        Assert.Equal("done", result.Metadata!["outcome"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies flow.if activates only the true branch.
    /// </summary>
    [Fact]
    public async Task FlowIfTrueActivatesOnlyTrue()
    {
        NodeHandlerResult result = await new FlowIfHandler().ExecuteAsync(Request("flow.if", new JsonObject { ["condition"] = true }), Context("flow.if"));

        Assert.Equal(["true"], result.Outputs.ActivatedControlOutputs);
    }

    /// <summary>
    /// Verifies flow.if activates only the false branch.
    /// </summary>
    [Fact]
    public async Task FlowIfFalseActivatesOnlyFalse()
    {
        NodeHandlerResult result = await new FlowIfHandler().ExecuteAsync(Request("flow.if", new JsonObject { ["condition"] = false }), Context("flow.if"));

        Assert.Equal(["false"], result.Outputs.ActivatedControlOutputs);
    }

    /// <summary>
    /// Verifies flow.if rejects a non-boolean materialized condition.
    /// </summary>
    [Fact]
    public async Task FlowIfRejectsNonBoolean()
    {
        NodeHandlerResult result = await new FlowIfHandler().ExecuteAsync(Request("flow.if", new JsonObject { ["condition"] = "yes" }), Context("flow.if"));

        Assert.Equal(NodeHandlerCompletionStatus.Failed, result.Status);
    }

    /// <summary>
    /// Verifies flow.switch selects the first deterministic matching case.
    /// </summary>
    [Fact]
    public async Task FlowSwitchSelectsFirstMatchingCase()
    {
        JsonObject parameters = new()
        {
            ["cases"] = new JsonArray
            {
                new JsonObject { ["id"] = "first", ["when"] = true },
                new JsonObject { ["id"] = "second", ["when"] = true },
            },
        };

        NodeHandlerResult result = await new FlowSwitchHandler().ExecuteAsync(Request("flow.switch", parameters), Context("flow.switch"));

        Assert.Equal(["first"], result.Outputs.ActivatedControlOutputs);
    }

    /// <summary>
    /// Verifies flow.switch selects default when no case matches.
    /// </summary>
    [Fact]
    public async Task FlowSwitchSelectsDefault()
    {
        JsonObject parameters = new()
        {
            ["cases"] = new JsonArray
            {
                new JsonObject { ["id"] = "first", ["when"] = false },
            },
        };

        NodeHandlerResult result = await new FlowSwitchHandler().ExecuteAsync(Request("flow.switch", parameters), Context("flow.switch"));

        Assert.Equal(["default"], result.Outputs.ActivatedControlOutputs);
    }

    /// <summary>
    /// Verifies flow.switch preserves dynamic port identity.
    /// </summary>
    [Fact]
    public async Task FlowSwitchPreservesDynamicPortIdentity()
    {
        JsonObject parameters = new()
        {
            ["cases"] = new JsonArray
            {
                new JsonObject { ["id"] = "customer_email", ["when"] = true },
            },
        };

        NodeHandlerResult result = await new FlowSwitchHandler().ExecuteAsync(Request("flow.switch", parameters), Context("flow.switch"));

        Assert.Equal(["customer_email"], result.Outputs.ActivatedControlOutputs);
    }

    /// <summary>
    /// Verifies interaction.request propagates cancellation when a host handler is supplied.
    /// </summary>
    [Fact]
    public async Task InteractionRequestPropagatesCancellationWhenImplemented()
    {
        InteractionRequestHandler handler = new(new CancellingInteractionHandler());
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await handler.ExecuteAsync(Request("interaction.request", new JsonObject { ["kind"] = "confirmation", ["prompt"] = "Continue?" }), Context("interaction.request"), cancellation.Token));
    }

    /// <summary>
    /// Verifies handlers expose exact built-in definition keys and workflow invocation remains runtime-owned.
    /// </summary>
    [Fact]
    public void HandlersExposeExactDefinitionKeys()
    {
        IReadOnlyList<INodeHandler> handlers = BuiltInRuntimeHandlers.Create();

        Assert.Equal(["core.end", "core.return", "core.start", "flow.foreach", "flow.if", "flow.repeat", "flow.switch", "flow.while"], handlers.Select(static handler => handler.Definition.Type));
        Assert.All(handlers, handler => Assert.True(BuiltInWorkflowNodeCatalog.Catalog.TryGetDefinition(handler.Definition.Type, handler.Definition.Version, out _)));
        Assert.DoesNotContain(handlers, static handler => handler.Definition.Type == "workflow.invoke");
    }

    /// <summary>
    /// Verifies immutable handler resolution rejects duplicates and performs exact lookup only.
    /// </summary>
    [Fact]
    public void ImmutableResolverRejectsDuplicatesAndUsesExactLookup()
    {
        ImmutableNodeHandlerResolver resolver = BuiltInRuntimeHandlers.CreateResolver();

        Assert.True(resolver.TryResolve(new WorkflowNodeDefinitionKey("flow.if", 1), out INodeHandler? handler));
        Assert.IsType<FlowIfHandler>(handler);
        Assert.False(resolver.TryResolve(new WorkflowNodeDefinitionKey("flow.if", 2), out _));
        Assert.Throws<ArgumentException>(() => new ImmutableNodeHandlerResolver([new CoreStartHandler(), new CoreStartHandler()]));
    }

    /// <summary>
    /// Verifies built-in handler assemblies do not reference browser automation or service-provider APIs.
    /// </summary>
    [Fact]
    public void HandlersContainNoBrowserOrServiceProviderDependency()
    {
        Assembly assembly = typeof(CoreStartHandler).Assembly;
        string[] referenced = assembly.GetReferencedAssemblies().Select(static name => name.Name ?? string.Empty).ToArray();

        Assert.DoesNotContain(referenced, static name => name.Contains("Playwright", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, static name => name.Contains("Selenium", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, static name => name.Contains("Puppeteer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, static name => name.Contains("FlaUI", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, static name => name.Contains("DependencyInjection", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("IServiceProvider", File.ReadAllText(typeof(CoreStartHandler).Assembly.Location.Replace(".dll", ".xml", StringComparison.Ordinal)));
    }

    private static NodeExecutionRequest Request(string nodeType, JsonObject? parameters = null)
    {
        NodeExecutionIdentity identity = new("execution", "invocation", null, "workflow", "node", new WorkflowNodeDefinitionKey(nodeType, 1), "plan", "step", 1);
        return new NodeExecutionRequest(identity, parameters);
    }

    private static INodeExecutionContext Context(string nodeType)
    {
        NodeExecutionIdentity identity = new("execution", "invocation", null, "workflow", "node", new WorkflowNodeDefinitionKey(nodeType, 1), "plan", "step", 1);
        return new TestNodeExecutionContext(identity);
    }

    private sealed class CancellingInteractionHandler : IWorkflowInteractionHandler
    {
        public ValueTask<WorkflowInteractionResponse> RequestAsync(WorkflowInteractionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new WorkflowInteractionResponse(request.RequestId, WorkflowInteractionResponseStatus.Submitted, true, JsonValue.Create(true), DateTimeOffset.UtcNow));
        }
    }

    private sealed class TestNodeExecutionContext(NodeExecutionIdentity identity) : INodeExecutionContext
    {
        public NodeExecutionIdentity Identity { get; } = identity;

        public INodeExecutionEventWriter Events { get; } = new TestEventWriter();

        public INodeResourceAccessor Resources { get; } = new TestResourceAccessor();

        public INodeLocatorAccessor Locators { get; } = RuntimeNodeLocatorAccessor.Empty;
    }

    private sealed class TestEventWriter : INodeExecutionEventWriter
    {
        public ValueTask WriteLogAsync(WorkflowLogLevel level, string message, JsonObject? data = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask ReportProgressAsync(double? progress, string? message = null, JsonObject? data = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask EmitOutputAsync(string channel, JsonNode? payload, string? recordKey = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class TestResourceAccessor : INodeResourceAccessor
    {
        public IReadOnlyList<NodeResourceBinding> Bindings { get; } = Array.AsReadOnly(Array.Empty<NodeResourceBinding>());

        public bool TryGetBinding(string slotName, out NodeResourceBinding? binding)
        {
            binding = null;
            return false;
        }

        public ValueTask<INodeResourceLease> AcquireAsync(string slotName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("No resources are available in tests.");
        }
    }
}
