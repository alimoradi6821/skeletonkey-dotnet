using SkeletonKey.Workflow.Connections;

namespace SkeletonKey.Workflow.Outputs;

/// <summary>
/// Declares one final or streamed workflow output.
/// </summary>
/// <remarks>
/// Constructors represent the language declaration without enforcing mode-specific semantic combinations.
/// Semantic validation reports invalid combinations such as value outputs without source endpoints or stream
/// outputs with source endpoints.
/// </remarks>
public sealed class WorkflowOutputDefinition
{
    /// <summary>
    /// Initializes a new workflow output declaration.
    /// </summary>
    /// <param name="mode">The output mode.</param>
    /// <param name="from">The source node output endpoint for single and collection outputs.</param>
    /// <param name="channel">The stream channel for streamed outputs.</param>
    /// <param name="description">Optional human-readable output description.</param>
    public WorkflowOutputDefinition(
        WorkflowOutputMode mode,
        WorkflowEndpoint? from = null,
        string? channel = null,
        string? description = null)
    {
        Mode = mode;
        From = from;
        Channel = channel;
        Description = description;
    }

    /// <summary>
    /// Gets the output mode.
    /// </summary>
    public WorkflowOutputMode Mode { get; }

    /// <summary>
    /// Gets the source node output endpoint for single and collection outputs.
    /// </summary>
    public WorkflowEndpoint? From { get; }

    /// <summary>
    /// Gets the stream channel for streamed outputs.
    /// </summary>
    public string? Channel { get; }

    /// <summary>
    /// Gets optional human-readable output description.
    /// </summary>
    public string? Description { get; }
}
