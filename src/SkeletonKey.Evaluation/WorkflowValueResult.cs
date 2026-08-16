using System.Text.Json.Nodes;

namespace SkeletonKey.Evaluation;

/// <summary>
/// Represents an immutable workflow value processing result.
/// </summary>
/// <remarks>
/// A successful result may contain non-null JSON or explicit JSON null. A failed result always contains a structured error.
/// JSON values are defensively cloned on input and output.
/// </remarks>
public sealed class WorkflowValueResult
{
    private readonly JsonNode? _value;

    private WorkflowValueResult(bool isSuccess, JsonNode? value, WorkflowValueError? error)
    {
        IsSuccess = isSuccess;
        _value = value?.DeepClone();
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether processing succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a defensive copy of the resulting JSON value, or <see langword="null" /> for explicit JSON null.
    /// </summary>
    public JsonNode? Value => _value?.DeepClone();

    /// <summary>
    /// Gets the structured error for failed results.
    /// </summary>
    public WorkflowValueError? Error { get; }

    /// <summary>
    /// Creates a successful value result.
    /// </summary>
    /// <param name="value">The JSON value, or <see langword="null" /> for explicit JSON null.</param>
    /// <returns>An immutable successful result.</returns>
    public static WorkflowValueResult Success(JsonNode? value)
    {
        return new WorkflowValueResult(true, value, null);
    }

    /// <summary>
    /// Creates a failed value result.
    /// </summary>
    /// <param name="error">The required structured error.</param>
    /// <returns>An immutable failed result.</returns>
    public static WorkflowValueResult Failure(WorkflowValueError error)
    {
        return new WorkflowValueResult(false, null, error);
    }
}
