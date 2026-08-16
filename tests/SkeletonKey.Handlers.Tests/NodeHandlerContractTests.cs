using System.Reflection;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;

namespace SkeletonKey.Handlers.Tests;

/// <summary>
/// Verifies node handler and resolver contracts.
/// </summary>
public sealed class NodeHandlerContractTests
{
    /// <summary>
    /// Verifies handler identity uses exact node definition keys.
    /// </summary>
    [Fact]
    public void HandlerUsesExactNodeDefinitionKey()
    {
        WorkflowNodeDefinitionKey definition = new("Core.Log", 7);
        INodeHandler handler = new TestHandler(definition);

        Assert.Equal(definition, handler.Definition);
        Assert.NotEqual(new WorkflowNodeDefinitionKey("core.log", 7), handler.Definition);
    }

    /// <summary>
    /// Verifies handler execution accepts an explicit cancellation token.
    /// </summary>
    [Fact]
    public void HandlerMethodAcceptsCancellation()
    {
        MethodInfo method = typeof(INodeHandler).GetMethod(nameof(INodeHandler.ExecuteAsync))!;
        ParameterInfo cancellation = method.GetParameters().Single(static parameter => parameter.ParameterType == typeof(CancellationToken));

        Assert.True(cancellation.HasDefaultValue);
    }

    /// <summary>
    /// Verifies resolver exposes exact lookup only and no implicit latest-version API.
    /// </summary>
    [Fact]
    public void ResolverIsExactAndHasNoImplicitLatestVersionApi()
    {
        MethodInfo[] methods = typeof(INodeHandlerResolver).GetMethods();

        Assert.Single(methods);
        Assert.Equal(nameof(INodeHandlerResolver.TryResolve), methods[0].Name);
        Assert.Equal(typeof(WorkflowNodeDefinitionKey), methods[0].GetParameters()[0].ParameterType);
        Assert.DoesNotContain(methods, static method => method.Name.Contains("Latest", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, static method => method.Name.Contains("Register", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies handler context exposes runtime-owned boundaries without a service provider.
    /// </summary>
    [Fact]
    public void ContextExposesRuntimeOwnedBoundaries()
    {
        Assert.Contains(typeof(INodeExecutionContext).GetProperties(), static property => property.PropertyType == typeof(INodeExecutionEventWriter));
        Assert.Contains(typeof(INodeExecutionContext).GetProperties(), static property => property.PropertyType == typeof(INodeResourceAccessor));
        Assert.DoesNotContain(typeof(INodeExecutionContext).GetProperties(), static property => property.PropertyType == typeof(IServiceProvider));
    }

    /// <summary>
    /// Verifies event writer methods use cancellation and do not expose full workflow event construction.
    /// </summary>
    [Fact]
    public void EventWriterUsesObservationMethodsWithCancellation()
    {
        MethodInfo[] methods = typeof(INodeExecutionEventWriter).GetMethods();

        Assert.Contains(methods, static method => method.Name == nameof(INodeExecutionEventWriter.WriteLogAsync) && method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(WorkflowLogLevel)));
        Assert.Contains(methods, static method => method.Name == nameof(INodeExecutionEventWriter.ReportProgressAsync));
        Assert.Contains(methods, static method => method.Name == nameof(INodeExecutionEventWriter.EmitOutputAsync) && method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(JsonNode)));
        Assert.All(methods, static method => Assert.Contains(method.GetParameters(), static parameter => parameter.ParameterType == typeof(CancellationToken)));
    }

    private sealed class TestHandler(WorkflowNodeDefinitionKey definition) : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = definition;

        public ValueTask<NodeHandlerResult> ExecuteAsync(
            NodeExecutionRequest request,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Success());
        }
    }
}
