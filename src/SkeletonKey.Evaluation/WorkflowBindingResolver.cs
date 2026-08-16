using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;
using SkeletonKey.Workflow.Bindings;

namespace SkeletonKey.Evaluation;

/// <summary>
/// Resolves structured workflow bindings over immutable workflow value context data.
/// </summary>
/// <remarks>
/// The resolver is stateless, deterministic, thread-safe, and performs no binding expression evaluation, resource resolution,
/// locator resolution, I/O, or runtime state mutation. Defaults are literal JSON and are defensively cloned.
/// </remarks>
public sealed class WorkflowBindingResolver : IWorkflowBindingResolver
{
    private readonly JsonPointerResolver _pointerResolver;

    /// <summary>
    /// Initializes a new workflow binding resolver.
    /// </summary>
    /// <param name="pointerResolver">The optional read-only JSON Pointer resolver.</param>
    public WorkflowBindingResolver(JsonPointerResolver? pointerResolver = null)
    {
        _pointerResolver = pointerResolver ?? new JsonPointerResolver();
    }

    /// <inheritdoc />
    public WorkflowValueResult Resolve(
        WorkflowBinding binding,
        WorkflowValueResolutionContext context,
        string jsonPath)
    {
        WorkflowValueResult source = ResolveSource(binding, context, jsonPath);
        if (!source.IsSuccess)
        {
            return ApplyMissing(binding, source.Error!, jsonPath);
        }

        WorkflowValueResult pointed = _pointerResolver.Resolve(source.Value, binding.Path, jsonPath);
        if (pointed.IsSuccess)
        {
            return pointed;
        }

        if (pointed.Error?.Code == WorkflowValueErrorCode.InvalidJsonPointer)
        {
            return pointed;
        }

        return ApplyMissing(binding, pointed.Error!, jsonPath);
    }

    private static WorkflowValueResult ResolveSource(WorkflowBinding binding, WorkflowValueResolutionContext context, string jsonPath)
    {
        return binding.Source switch
        {
            WorkflowBindingSource.Input => ResolveInput(binding, context, jsonPath),
            WorkflowBindingSource.Variable => ResolveVariable(binding, context, jsonPath),
            WorkflowBindingSource.Node => ResolveNode(binding, context, jsonPath),
            WorkflowBindingSource.Iteration => ResolveIteration(binding, context, jsonPath),
            _ => WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.MissingBindingSourceValue, "Binding source is unsupported.", jsonPath)),
        };
    }

    private static WorkflowValueResult ResolveInput(WorkflowBinding binding, WorkflowValueResolutionContext context, string jsonPath)
    {
        return binding.Name is not null && context.TryGetInput(binding.Name, out JsonNode? value)
            ? WorkflowValueResult.Success(value)
            : WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.UnknownInput, "Binding input source was not found.", jsonPath));
    }

    private static WorkflowValueResult ResolveVariable(WorkflowBinding binding, WorkflowValueResolutionContext context, string jsonPath)
    {
        return binding.Name is not null && context.TryGetVariable(binding.Name, out JsonNode? value)
            ? WorkflowValueResult.Success(value)
            : WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.UnknownVariable, "Binding variable source was not found.", jsonPath));
    }

    private static WorkflowValueResult ResolveNode(WorkflowBinding binding, WorkflowValueResolutionContext context, string jsonPath)
    {
        if (binding.Node is null)
        {
            return WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.UnknownNode, "Binding node source was not found.", jsonPath));
        }

        if (!context.TryGetNode(binding.Node, out NodePortValueMap? node) || node is null)
        {
            return WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.UnknownNode, "Binding node source was not found.", jsonPath));
        }

        if (binding.Port is null)
        {
            return WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.UnknownNodeOutputPort, "Binding node output port was not found.", jsonPath));
        }

        if (!node.Values.TryGetValue(binding.Port, out NodePortValueSet? values) || values is null)
        {
            return WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.UnknownNodeOutputPort, "Binding node output port was not found.", jsonPath));
        }

        return WorkflowValueResolutionContext.TryProjectPortValue(values, out JsonNode? projected)
            ? WorkflowValueResult.Success(projected)
            : WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.MissingBindingSourceValue, "Binding node output port has no values.", jsonPath));
    }

    private static WorkflowValueResult ResolveIteration(WorkflowBinding binding, WorkflowValueResolutionContext context, string jsonPath)
    {
        if (binding.Iteration is null || !context.TryGetIteration(binding.Iteration, out WorkflowIterationContext? iteration) || iteration is null)
        {
            return WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.UnknownIteration, "Binding iteration source was not found.", jsonPath));
        }

        return WorkflowValueResult.Success(WorkflowValueResolutionContext.ProjectIteration(iteration));
    }

    private static WorkflowValueResult ApplyMissing(WorkflowBinding binding, WorkflowValueError originalError, string jsonPath)
    {
        return binding.OnMissing switch
        {
            WorkflowBindingMissingBehavior.Null => WorkflowValueResult.Success(null),
            WorkflowBindingMissingBehavior.Default when binding.HasDefault => WorkflowValueResult.Success(binding.Default),
            WorkflowBindingMissingBehavior.Default => WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.MissingBindingSourceValue, "Binding default is not available.", jsonPath)),
            _ => WorkflowValueResult.Failure(originalError),
        };
    }
}
