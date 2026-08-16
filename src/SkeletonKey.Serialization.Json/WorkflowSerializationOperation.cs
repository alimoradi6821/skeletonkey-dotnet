namespace SkeletonKey.Serialization.Json;

/// <summary>
/// Identifies the serializer operation that failed.
/// </summary>
public enum WorkflowSerializationOperation
{
    /// <summary>
    /// A workflow document was being serialized to JSON.
    /// </summary>
    Serialize,

    /// <summary>
    /// Workflow JSON was being deserialized to a document.
    /// </summary>
    Deserialize,

    /// <summary>
    /// A workflow JSON file was being read.
    /// </summary>
    ReadFile,

    /// <summary>
    /// A workflow JSON file was being written.
    /// </summary>
    WriteFile,
}
