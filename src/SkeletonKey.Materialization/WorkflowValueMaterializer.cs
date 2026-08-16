using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Binding;
using SkeletonKey.Evaluation;
using SkeletonKey.Expressions;
using SkeletonKey.Locators;
using SkeletonKey.Resources;
using SkeletonKey.Workflow.Bindings;

namespace SkeletonKey.Materialization;

/// <summary>
/// Recursively materializes workflow-value JSON into deterministic handler-ready JSON.
/// </summary>
/// <remarks>
/// The materializer is stateless, deterministic, thread-safe, and performs no workflow execution, node execution,
/// resource resolution, locator resolution, host access, I/O, clock access, randomness, or handler invocation.
/// Source JSON is never mutated and returned JSON is defensively owned.
/// </remarks>
public sealed class WorkflowValueMaterializer : IWorkflowValueMaterializer
{
    private readonly WorkflowBindingReader _bindingReader;
    private readonly WorkflowExpressionReader _expressionReader;
    private readonly WorkflowResourceReferenceReader _resourceReader;
    private readonly LocatorReferenceReader _locatorReader;
    private readonly IWorkflowBindingResolver _bindingResolver;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new workflow value materializer.
    /// </summary>
    /// <param name="bindingResolver">Optional binding resolver.</param>
    /// <param name="expressionEvaluator">Optional expression evaluator.</param>
    public WorkflowValueMaterializer(
        IWorkflowBindingResolver? bindingResolver = null,
        IWorkflowExpressionEvaluator? expressionEvaluator = null)
    {
        _bindingReader = new WorkflowBindingReader();
        _expressionReader = new WorkflowExpressionReader();
        _resourceReader = new WorkflowResourceReferenceReader();
        _locatorReader = new LocatorReferenceReader();
        _bindingResolver = bindingResolver ?? new WorkflowBindingResolver();
        _expressionEvaluator = expressionEvaluator ?? new WorkflowExpressionEvaluator();
    }

    /// <inheritdoc />
    public WorkflowValueResult Materialize(
        JsonNode? workflowValue,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits? limits = null,
        string jsonPath = "")
    {
        WorkflowValueProcessingLimits actualLimits = limits ?? WorkflowValueProcessingLimits.Default;
        WorkflowValueResult result = MaterializeCore(workflowValue, context, actualLimits, jsonPath, 0);
        return result.IsSuccess ? EnforceResultLimits(result.Value, actualLimits, jsonPath, 0) : result;
    }

    private WorkflowValueResult MaterializeCore(
        JsonNode? workflowValue,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits limits,
        string jsonPath,
        int depth)
    {
        if (depth > limits.MaximumMaterializationDepth)
        {
            return Failure(WorkflowValueErrorCode.MaterializationDepthLimitExceeded, "Materialization depth limit was exceeded.", jsonPath);
        }

        if (workflowValue is null)
        {
            return WorkflowValueResult.Success(null);
        }

        if (workflowValue is JsonValue)
        {
            return WorkflowValueResult.Success(workflowValue);
        }

        if (workflowValue is JsonArray array)
        {
            if (array.Count > limits.MaximumCollectionItems)
            {
                return Failure(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Array item limit was exceeded.", jsonPath);
            }

            JsonArray materialized = [];
            for (int index = 0; index < array.Count; index++)
            {
                WorkflowValueResult item = MaterializeCore(array[index], context, limits, Combine(jsonPath, index), depth + 1);
                if (!item.IsSuccess)
                {
                    return item;
                }

                materialized.Add(item.Value);
            }

            return WorkflowValueResult.Success(materialized);
        }

        JsonObject jsonObject = workflowValue.AsObject();
        if (jsonObject.Count > limits.MaximumCollectionItems)
        {
            return Failure(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Object property limit was exceeded.", jsonPath);
        }

        WorkflowValueResult? wrapper = TryMaterializeWrapper(jsonObject, context, limits, jsonPath, depth);
        if (wrapper is not null)
        {
            return wrapper;
        }

        JsonObject result = [];
        foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
        {
            if (IsReserved(property.Key))
            {
                return Failure(WorkflowValueErrorCode.MalformedWorkflowValueWrapper, $"Reserved workflow value property '{property.Key}' must be represented as its wrapper.", Combine(jsonPath, property.Key));
            }

            WorkflowValueResult materialized = MaterializeCore(property.Value, context, limits, Combine(jsonPath, property.Key), depth + 1);
            if (!materialized.IsSuccess)
            {
                return materialized;
            }

            result[property.Key] = materialized.Value;
        }

        return WorkflowValueResult.Success(result);
    }

    private WorkflowValueResult? TryMaterializeWrapper(
        JsonObject jsonObject,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits limits,
        string jsonPath,
        int depth)
    {
        int reservedCount = jsonObject.Count(property => IsReserved(property.Key));
        if (reservedCount == 0)
        {
            return null;
        }

        if (jsonObject.Count != 1 || reservedCount != 1)
        {
            return Failure(WorkflowValueErrorCode.MalformedWorkflowValueWrapper, "Workflow-value wrapper must contain exactly one reserved property.", jsonPath);
        }

        if (jsonObject.ContainsKey("$literal"))
        {
            return WorkflowValueResult.Success(jsonObject["$literal"]);
        }

        if (jsonObject.ContainsKey("$binding"))
        {
            try
            {
                WorkflowBinding binding = _bindingReader.Read(jsonObject);
                return _bindingResolver.Resolve(binding, context, jsonPath);
            }
            catch (WorkflowBindingFormatException exception)
            {
                return Failure(WorkflowValueErrorCode.MalformedWorkflowValueWrapper, exception.Message, exception.JsonPath);
            }
        }

        if (jsonObject.ContainsKey("$expression"))
        {
            try
            {
                string text = _expressionReader.ReadText(jsonObject);
                return _expressionEvaluator.Evaluate(text, context, limits);
            }
            catch (WorkflowExpressionFormatException exception)
            {
                return Failure(WorkflowValueErrorCode.MalformedWorkflowValueWrapper, exception.Message, exception.JsonPath);
            }
        }

        if (jsonObject.ContainsKey("$resource"))
        {
            try
            {
                _resourceReader.Read(jsonObject);
            }
            catch (WorkflowResourceReferenceFormatException exception)
            {
                return Failure(WorkflowValueErrorCode.MalformedWorkflowValueWrapper, exception.Message, exception.JsonPath);
            }

            return Failure(WorkflowValueErrorCode.ResourceReferenceCannotBeJsonMaterialized, "Resource references require scoped runtime resource binding and cannot be JSON-materialized.", jsonPath);
        }

        if (jsonObject.ContainsKey("$locator"))
        {
            try
            {
                _locatorReader.Read(jsonObject);
            }
            catch (LocatorReferenceFormatException exception)
            {
                return Failure(WorkflowValueErrorCode.MalformedWorkflowValueWrapper, exception.Message, exception.JsonPath);
            }

            return Failure(WorkflowValueErrorCode.LocatorReferenceCannotBeJsonMaterialized, "Locator references require future locator-aware preparation and cannot be JSON-materialized.", jsonPath);
        }

        return Failure(WorkflowValueErrorCode.MalformedWorkflowValueWrapper, "Workflow-value wrapper is malformed.", jsonPath);
    }

    private static WorkflowValueResult EnforceResultLimits(JsonNode? value, WorkflowValueProcessingLimits limits, string jsonPath, int depth)
    {
        if (depth > limits.MaximumResultDepth)
        {
            return Failure(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Result depth limit was exceeded.", jsonPath);
        }

        if (value is null)
        {
            return WorkflowValueResult.Success(null);
        }

        if (value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Length > limits.MaximumStringLength)
        {
            return Failure(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Result string length limit was exceeded.", jsonPath);
        }

        if (value is JsonArray array)
        {
            if (array.Count > limits.MaximumCollectionItems)
            {
                return Failure(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Result array item limit was exceeded.", jsonPath);
            }

            for (int index = 0; index < array.Count; index++)
            {
                WorkflowValueResult child = EnforceResultLimits(array[index], limits, Combine(jsonPath, index), depth + 1);
                if (!child.IsSuccess)
                {
                    return child;
                }
            }
        }

        if (value is JsonObject jsonObject)
        {
            if (jsonObject.Count > limits.MaximumCollectionItems)
            {
                return Failure(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Result object property limit was exceeded.", jsonPath);
            }

            foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
            {
                WorkflowValueResult child = EnforceResultLimits(property.Value, limits, Combine(jsonPath, property.Key), depth + 1);
                if (!child.IsSuccess)
                {
                    return child;
                }
            }
        }

        return WorkflowValueResult.Success(value);
    }

    private static bool IsReserved(string propertyName)
    {
        return propertyName is "$literal" or "$binding" or "$expression" or "$resource" or "$locator";
    }

    private static WorkflowValueResult Failure(string code, string message, string path)
    {
        return WorkflowValueResult.Failure(new WorkflowValueError(code, message, path));
    }

    private static string Combine(string path, int index)
    {
        return path.Length == 0
            ? "/" + index.ToString(CultureInfo.InvariantCulture)
            : path + "/" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static string Combine(string path, string token)
    {
        string escaped = token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        return path.Length == 0 ? "/" + escaped : path + "/" + escaped;
    }
}
