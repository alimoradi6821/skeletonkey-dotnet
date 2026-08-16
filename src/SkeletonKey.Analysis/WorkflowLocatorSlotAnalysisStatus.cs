namespace SkeletonKey.Analysis;

/// <summary>
/// Describes the static analysis status for one declared node locator slot.
/// </summary>
public enum WorkflowLocatorSlotAnalysisStatus
{
    /// <summary>The locator slot is satisfied.</summary>
    Satisfied,

    /// <summary>A required locator slot is missing.</summary>
    MissingRequiredLocator,

    /// <summary>The locator reference wrapper is malformed.</summary>
    InvalidLocatorReference,

    /// <summary>The locator document could not be resolved.</summary>
    UnknownLocatorDocument,

    /// <summary>The locator ID could not be resolved.</summary>
    UnknownLocatorId,

    /// <summary>The resolved locator cardinality is not accepted by the slot.</summary>
    CardinalityMismatch,
}
