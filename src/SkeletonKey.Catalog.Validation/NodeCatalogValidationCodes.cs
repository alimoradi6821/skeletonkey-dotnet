namespace SkeletonKey.Catalog.Validation;

/// <summary>
/// Defines stable semantic validation codes for node catalog documents.
/// </summary>
public static class NodeCatalogValidationCodes
{
    /// <summary>Catalog ID is invalid.</summary>
    public const string InvalidCatalogId = "SKC1001";

    /// <summary>Catalog version is invalid.</summary>
    public const string InvalidCatalogVersion = "SKC1002";

    /// <summary>Catalog definitions are missing.</summary>
    public const string MissingDefinitions = "SKC1003";

    /// <summary>Node type is invalid.</summary>
    public const string InvalidNodeType = "SKC1004";

    /// <summary>Node type version is invalid.</summary>
    public const string InvalidNodeVersion = "SKC1005";

    /// <summary>Duplicate node definition identity.</summary>
    public const string DuplicateNodeDefinition = "SKC1006";

    /// <summary>Port name is invalid.</summary>
    public const string InvalidPortName = "SKC1007";

    /// <summary>Port direction is invalid.</summary>
    public const string InvalidPortDirection = "SKC1008";

    /// <summary>Capability ID is invalid.</summary>
    public const string InvalidCapabilityId = "SKC1009";

    /// <summary>Duplicate capability.</summary>
    public const string DuplicateCapability = "SKC1010";

    /// <summary>Resource slot is invalid.</summary>
    public const string InvalidResourceSlot = "SKC1011";

    /// <summary>Resource kind is invalid.</summary>
    public const string InvalidResourceKind = "SKC1012";

    /// <summary>Dynamic port rule is invalid.</summary>
    public const string InvalidDynamicPortRule = "SKC1013";

    /// <summary>Deprecation metadata is invalid.</summary>
    public const string InvalidDeprecation = "SKC1014";

    /// <summary>Locator slot is invalid.</summary>
    public const string InvalidLocatorSlot = "SKC1015";
}
