namespace SkeletonKey.Analysis;

/// <summary>
/// Defines stable catalog-aware workflow analysis issue codes.
/// </summary>
public static class WorkflowAnalysisCodes
{
    /// <summary>
    /// The node type is not available in the supplied catalog.
    /// </summary>
    public const string UnknownNodeType = "SKA1001";

    /// <summary>
    /// The node type exists, but the requested version is not available in the supplied catalog.
    /// </summary>
    public const string UnknownNodeVersion = "SKA1002";

    /// <summary>
    /// Node parameters do not satisfy the catalog parameter contract.
    /// </summary>
    public const string InvalidNodeParameters = "SKA1008";

    /// <summary>
    /// A connection source port is not declared by the source node definition.
    /// </summary>
    public const string UnknownSourcePort = "SKA1003";

    /// <summary>
    /// A connection target port is not declared by the target node definition.
    /// </summary>
    public const string UnknownTargetPort = "SKA1004";

    /// <summary>
    /// A connection endpoint references a port with the wrong direction.
    /// </summary>
    public const string InvalidPortDirection = "SKA1009";

    /// <summary>
    /// A connection source and target have incompatible catalog roles.
    /// </summary>
    public const string IncompatiblePortRoles = "SKA1010";

    /// <summary>
    /// A dynamic port declaration cannot be analyzed.
    /// </summary>
    public const string InvalidDynamicPortDeclaration = "SKA1011";

    /// <summary>
    /// A node requires a workflow resource that is not declared.
    /// </summary>
    public const string MissingRequiredResource = "SKA1005";

    /// <summary>
    /// A declared workflow resource does not match the catalog-required kind.
    /// </summary>
    public const string ResourceKindMismatch = "SKA1006";

    /// <summary>
    /// A declared workflow resource does not provide a catalog-required capability.
    /// </summary>
    public const string MissingResourceCapability = "SKA1007";

    /// <summary>
    /// A workflow resource reference cannot be matched to a required resource slot.
    /// </summary>
    public const string InvalidResourceReference = "SKA1012";

    /// <summary>
    /// Catalog definitions conflict before analysis can proceed.
    /// </summary>
    public const string CatalogDefinitionConflict = "SKA1013";

    /// <summary>
    /// A node definition is deprecated.
    /// </summary>
    public const string DeprecatedNodeDefinition = "SKA1014";

    /// <summary>
    /// A port allows only one compatible incoming connection.
    /// </summary>
    public const string PortMultiplicityViolation = "SKA1015";

    /// <summary>
    /// A declared resource access mode is incompatible with a catalog slot.
    /// </summary>
    public const string ResourceAccessMismatch = "SKA1016";

    /// <summary>
    /// Semantic validation produced errors before catalog-aware analysis.
    /// </summary>
    public const string SemanticValidationError = "SKA1017";

    /// <summary>
    /// The analyzer reached its configured issue limit.
    /// </summary>
    public const string IssueLimitReached = "SKA1018";

    /// <summary>
    /// A node requires a locator slot that is not supplied.
    /// </summary>
    public const string MissingRequiredLocator = "SKA1019";

    /// <summary>
    /// A workflow locator reference cannot be matched to a required locator slot.
    /// </summary>
    public const string InvalidLocatorReference = "SKA1020";

    /// <summary>
    /// A locator document or locator ID cannot be resolved.
    /// </summary>
    public const string LocatorResolutionFailed = "SKA1021";

    /// <summary>
    /// A resolved locator cardinality is incompatible with a catalog slot.
    /// </summary>
    public const string LocatorCardinalityMismatch = "SKA1022";
}
