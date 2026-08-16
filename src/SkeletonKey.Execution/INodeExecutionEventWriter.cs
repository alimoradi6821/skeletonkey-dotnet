using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;

namespace SkeletonKey.Execution;

/// <summary>
/// Defines runtime-owned event-writing operations available to node handlers.
/// </summary>
/// <remarks>
/// Handlers request observations through this interface, but a future runtime owns event IDs, sequence numbers, timestamps,
/// execution identity enrichment, redaction, and dispatch. JSON payload ownership is transferred defensively by implementations.
/// </remarks>
public interface INodeExecutionEventWriter
{
    /// <summary>
    /// Writes a host-neutral log observation for the current node attempt.
    /// </summary>
    /// <param name="level">The workflow log level.</param>
    /// <param name="message">The log message. Handlers must not include secrets unless the workflow explicitly requires secret output.</param>
    /// <param name="data">Optional host-neutral JSON data.</param>
    /// <param name="cancellationToken">A token the future runtime supplies for cancellation.</param>
    /// <returns>A task that completes when the observation request has been accepted by the runtime boundary.</returns>
    public ValueTask WriteLogAsync(
        WorkflowLogLevel level,
        string message,
        JsonObject? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports node progress without selecting event identity, timestamp, or root sequence values.
    /// </summary>
    /// <param name="progress">Optional normalized progress value supplied by the handler.</param>
    /// <param name="message">Optional progress message.</param>
    /// <param name="data">Optional host-neutral JSON data.</param>
    /// <param name="cancellationToken">A token the future runtime supplies for cancellation.</param>
    /// <returns>A task that completes when the observation request has been accepted by the runtime boundary.</returns>
    public ValueTask ReportProgressAsync(
        double? progress,
        string? message = null,
        JsonObject? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emits a streamed node output payload through a runtime-owned workflow event boundary.
    /// </summary>
    /// <param name="channel">The output channel or port name.</param>
    /// <param name="payload">The optional JSON payload, including explicit JSON null when <see langword="null" /> is supplied as a value.</param>
    /// <param name="recordKey">An optional stable record key supplied by the handler.</param>
    /// <param name="cancellationToken">A token the future runtime supplies for cancellation.</param>
    /// <returns>A task that completes when the observation request has been accepted by the runtime boundary.</returns>
    public ValueTask EmitOutputAsync(
        string channel,
        JsonNode? payload,
        string? recordKey = null,
        CancellationToken cancellationToken = default);
}
