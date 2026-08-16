namespace SkeletonKey.Workflow.Inputs;

/// <summary>
/// Defines the primitive input shapes supported by workflow documents.
/// </summary>
public enum WorkflowInputType
{
    /// <summary>
    /// A text value.
    /// </summary>
    String,

    /// <summary>
    /// A whole-number value.
    /// </summary>
    Integer,

    /// <summary>
    /// A numeric value that may include a fractional component.
    /// </summary>
    Number,

    /// <summary>
    /// A true or false value.
    /// </summary>
    Boolean,

    /// <summary>
    /// A JSON object value.
    /// </summary>
    Object,

    /// <summary>
    /// A JSON array value.
    /// </summary>
    Array,
}
