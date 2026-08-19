using System.Text.Json.Nodes;

namespace SkeletonKey.Runtime.Resources;

/// <summary>Contains bounded provider-owned state used to reconstruct one runtime resource.</summary>
public sealed class WorkflowRuntimeResourceCheckpointState
{
    private readonly JsonObject _payload;

    /// <summary>Initializes an immutable resource checkpoint state.</summary>
    public WorkflowRuntimeResourceCheckpointState(string formatVersion, JsonObject payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formatVersion);
        ArgumentNullException.ThrowIfNull(payload);
        FormatVersion = formatVersion;
        _payload = (JsonObject)payload.DeepClone();
    }

    /// <summary>Gets the provider-specific state format version.</summary>
    public string FormatVersion { get; }

    /// <summary>Gets a defensive clone of the provider-specific state payload.</summary>
    public JsonObject Payload => (JsonObject)_payload.DeepClone();
}
