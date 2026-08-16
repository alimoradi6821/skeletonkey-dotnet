namespace SkeletonKey.Validation;

/// <summary>
/// Defines stable semantic validation diagnostic codes for workflow language 0.1.
/// </summary>
public static class WorkflowValidationCodes
{
    /// <summary>Invalid schema URI.</summary>
    public const string InvalidSchemaUri = "SKW2001";

    /// <summary>Invalid specification version.</summary>
    public const string InvalidSpecificationVersion = "SKW2002";

    /// <summary>Workflow ID is required.</summary>
    public const string WorkflowIdRequired = "SKW2003";

    /// <summary>Invalid workflow ID.</summary>
    public const string InvalidWorkflowId = "SKW2004";

    /// <summary>Workflow name is required.</summary>
    public const string WorkflowNameRequired = "SKW2005";

    /// <summary>Invalid input name.</summary>
    public const string InvalidInputName = "SKW2101";

    /// <summary>Required input declares a default.</summary>
    public const string RequiredInputDeclaresDefault = "SKW2102";

    /// <summary>Input default type mismatch.</summary>
    public const string InputDefaultTypeMismatch = "SKW2103";

    /// <summary>Invalid variable name.</summary>
    public const string InvalidVariableName = "SKW2201";

    /// <summary>Workflow has no nodes.</summary>
    public const string WorkflowHasNoNodes = "SKW2301";

    /// <summary>Node ID is required.</summary>
    public const string NodeIdRequired = "SKW2302";

    /// <summary>Invalid node ID.</summary>
    public const string InvalidNodeId = "SKW2303";

    /// <summary>Duplicate node ID.</summary>
    public const string DuplicateNodeId = "SKW2304";

    /// <summary>Node type is required.</summary>
    public const string NodeTypeRequired = "SKW2305";

    /// <summary>Invalid node type.</summary>
    public const string InvalidNodeType = "SKW2306";

    /// <summary>Invalid node type version.</summary>
    public const string InvalidNodeTypeVersion = "SKW2307";

    /// <summary>Invalid start node count.</summary>
    public const string InvalidStartNodeCount = "SKW2308";

    /// <summary>Start node is disabled.</summary>
    public const string StartNodeIsDisabled = "SKW2309";

    /// <summary>Source node is required.</summary>
    public const string SourceNodeRequired = "SKW2401";

    /// <summary>Target node is required.</summary>
    public const string TargetNodeRequired = "SKW2402";

    /// <summary>Connection references unknown source node.</summary>
    public const string UnknownSourceNode = "SKW2403";

    /// <summary>Connection references unknown target node.</summary>
    public const string UnknownTargetNode = "SKW2404";

    /// <summary>Invalid source port.</summary>
    public const string InvalidSourcePort = "SKW2405";

    /// <summary>Invalid target port.</summary>
    public const string InvalidTargetPort = "SKW2406";

    /// <summary>Duplicate connection.</summary>
    public const string DuplicateConnection = "SKW2407";

    /// <summary>Incoming connection to start node.</summary>
    public const string IncomingConnectionToStartNode = "SKW2408";

    /// <summary>Outgoing connection from end node.</summary>
    public const string OutgoingConnectionFromEndNode = "SKW2409";

    /// <summary>Unreachable node.</summary>
    public const string UnreachableNode = "SKW2501";

    /// <summary>Invalid timeout.</summary>
    public const string InvalidTimeout = "SKW2601";

    /// <summary>Invalid retry attempt count.</summary>
    public const string InvalidRetryAttemptCount = "SKW2602";

    /// <summary>Invalid retry delay.</summary>
    public const string InvalidRetryDelay = "SKW2603";

    /// <summary>Invalid retry backoff.</summary>
    public const string InvalidRetryBackoff = "SKW2604";

    /// <summary>Invalid retry maximum delay.</summary>
    public const string InvalidRetryMaximumDelay = "SKW2605";

    /// <summary>Maximum delay is less than delay.</summary>
    public const string MaximumDelayLessThanDelay = "SKW2606";

    /// <summary>Designer position references unknown node.</summary>
    public const string DesignerPositionUnknownNode = "SKW2701";

    /// <summary>Designer size references unknown node.</summary>
    public const string DesignerSizeUnknownNode = "SKW2702";

    /// <summary>Invalid designer position.</summary>
    public const string InvalidDesignerPosition = "SKW2703";

    /// <summary>Invalid designer size.</summary>
    public const string InvalidDesignerSize = "SKW2704";

    /// <summary>Invalid workflow output name.</summary>
    public const string InvalidWorkflowOutputName = "SKW2801";

    /// <summary>Value output requires a source endpoint.</summary>
    public const string ValueOutputRequiresSourceEndpoint = "SKW2802";

    /// <summary>Stream output requires a channel.</summary>
    public const string StreamOutputRequiresChannel = "SKW2803";

    /// <summary>Output declaration contains incompatible properties.</summary>
    public const string OutputIncompatibleProperties = "SKW2804";

    /// <summary>Output references unknown source node.</summary>
    public const string OutputUnknownSourceNode = "SKW2805";

    /// <summary>Invalid output source port.</summary>
    public const string InvalidOutputSourcePort = "SKW2806";

    /// <summary>Invalid output channel name.</summary>
    public const string InvalidOutputChannelName = "SKW2807";

    /// <summary>
    /// A workflow invocation node is missing its workflow reference.
    /// </summary>
    public const string MissingInvocationWorkflowReference = "SKW2901";

    /// <summary>
    /// A referenced workflow ID has an invalid format.
    /// </summary>
    public const string InvalidReferencedWorkflowId = "SKW2902";

    /// <summary>
    /// A referenced workflow version is not an exact Semantic Version 2.0 value.
    /// </summary>
    public const string InvalidReferencedWorkflowVersion = "SKW2903";

    /// <summary>
    /// A workflow invocation input name has an invalid format.
    /// </summary>
    public const string InvalidInvocationInputName = "SKW2904";

    /// <summary>
    /// A reserved binding wrapper is malformed.
    /// </summary>
    public const string MalformedBindingWrapper = "SKW2905";

    /// <summary>
    /// A binding source is unknown.
    /// </summary>
    public const string UnknownBindingSource = "SKW2906";

    /// <summary>
    /// A binding references an unknown workflow input.
    /// </summary>
    public const string UnknownWorkflowInputBinding = "SKW2907";

    /// <summary>
    /// A binding references an unknown workflow variable.
    /// </summary>
    public const string UnknownWorkflowVariableBinding = "SKW2908";

    /// <summary>
    /// A binding references an unknown workflow node.
    /// </summary>
    public const string UnknownNodeBinding = "SKW2909";

    /// <summary>
    /// A node parameter binding references the same node.
    /// </summary>
    public const string SelfReferencingNodeBinding = "SKW2910";

    /// <summary>
    /// A node binding port has an invalid format.
    /// </summary>
    public const string InvalidNodeBindingPort = "SKW2911";

    /// <summary>
    /// A binding path is not a valid read-only RFC 6901 JSON Pointer.
    /// </summary>
    public const string InvalidBindingJsonPointer = "SKW2912";

    /// <summary>
    /// A binding missing-value configuration is invalid.
    /// </summary>
    public const string InvalidBindingMissingValueConfiguration = "SKW2913";

    /// <summary>
    /// A reserved literal wrapper is malformed.
    /// </summary>
    public const string InvalidLiteralWrapper = "SKW2914";

    /// <summary>
    /// A workflow invocation stream policy is invalid.
    /// </summary>
    public const string InvalidInvocationStreamPolicy = "SKW2915";

    /// <summary>
    /// A workflow invocation stream channel has an invalid format.
    /// </summary>
    public const string InvalidInvocationStreamChannel = "SKW2916";

    /// <summary>
    /// A workflow invocation stream mapping targets an undeclared parent stream channel.
    /// </summary>
    public const string UndeclaredParentStreamChannel = "SKW2917";

    /// <summary>
    /// A workflow invocation node declares an unsupported type version.
    /// </summary>
    public const string UnsupportedInvocationNodeVersion = "SKW2918";

    /// <summary>
    /// A reserved expression wrapper is malformed.
    /// </summary>
    public const string MalformedExpressionWrapper = "SKW3001";

    /// <summary>
    /// Expression text cannot be parsed.
    /// </summary>
    public const string ExpressionSyntaxError = "SKW3002";

    /// <summary>
    /// An expression references an undeclared workflow input.
    /// </summary>
    public const string UnknownExpressionInput = "SKW3003";

    /// <summary>
    /// An expression references an undeclared workflow variable.
    /// </summary>
    public const string UnknownExpressionVariable = "SKW3004";

    /// <summary>
    /// An expression references an unknown workflow node.
    /// </summary>
    public const string UnknownExpressionNode = "SKW3005";

    /// <summary>
    /// A node parameter expression references the same node.
    /// </summary>
    public const string SelfReferencingExpressionNode = "SKW3006";

    /// <summary>
    /// An expression or binding references an unknown or non-iteration loop node.
    /// </summary>
    public const string UnknownIterationReference = "SKW3007";

    /// <summary>
    /// An iteration binding has missing or incompatible properties.
    /// </summary>
    public const string InvalidIterationBindingShape = "SKW3008";

    /// <summary>
    /// An expression calls a function outside the version 0.1 allowlist.
    /// </summary>
    public const string UnknownExpressionFunction = "SKW3009";

    /// <summary>
    /// A reserved control-flow node declares an unsupported type version.
    /// </summary>
    public const string UnsupportedControlNodeVersion = "SKW3010";

    /// <summary>
    /// A reserved control node has invalid parameter shape.
    /// </summary>
    public const string InvalidControlNodeParameterShape = "SKW3011";

    /// <summary>
    /// A control condition is not a boolean literal, binding, or expression.
    /// </summary>
    public const string InvalidConditionValue = "SKW3012";

    /// <summary>
    /// A switch node has no cases.
    /// </summary>
    public const string MissingSwitchCases = "SKW3013";

    /// <summary>
    /// A switch case ID is invalid.
    /// </summary>
    public const string InvalidSwitchCaseId = "SKW3014";

    /// <summary>
    /// A switch case ID is duplicated.
    /// </summary>
    public const string DuplicateSwitchCaseId = "SKW3015";

    /// <summary>
    /// A foreach execution policy is invalid.
    /// </summary>
    public const string InvalidForEachExecutionPolicy = "SKW3016";

    /// <summary>
    /// A repeat count is invalid.
    /// </summary>
    public const string InvalidRepeatCount = "SKW3017";

    /// <summary>
    /// A while maxIterations value is invalid.
    /// </summary>
    public const string InvalidWhileIterationLimit = "SKW3018";

    /// <summary>
    /// A loop connection uses an unsupported loop control port.
    /// </summary>
    public const string InvalidLoopControlPort = "SKW3019";

    /// <summary>
    /// A conditional connection uses an unsupported output port.
    /// </summary>
    public const string InvalidConditionalOutputPort = "SKW3020";

    /// <summary>
    /// A return outcome declaration is invalid.
    /// </summary>
    public const string InvalidReturnOutcome = "SKW3021";

    /// <summary>
    /// A core.return node has an outgoing connection.
    /// </summary>
    public const string OutgoingConnectionFromReturn = "SKW3022";

    /// <summary>
    /// A reserved control node receives a connection on an unsupported input port.
    /// </summary>
    public const string InvalidReservedControlInputPort = "SKW3023";

    /// <summary>Invalid workflow resource name.</summary>
    public const string InvalidWorkflowResourceName = "SKW3101";

    /// <summary>Invalid workflow resource kind.</summary>
    public const string InvalidWorkflowResourceKind = "SKW3102";

    /// <summary>Invalid resource capability ID.</summary>
    public const string InvalidResourceCapabilityId = "SKW3103";

    /// <summary>Duplicate resource capability.</summary>
    public const string DuplicateResourceCapability = "SKW3104";

    /// <summary>Invalid standard resource constraints.</summary>
    public const string InvalidStandardResourceConstraints = "SKW3105";

    /// <summary>Malformed resource reference wrapper.</summary>
    public const string MalformedResourceReferenceWrapper = "SKW3106";

    /// <summary>Unknown workflow resource reference.</summary>
    public const string UnknownWorkflowResourceReference = "SKW3107";

    /// <summary>Invalid invocation resource mapping name.</summary>
    public const string InvalidInvocationResourceMappingName = "SKW3108";

    /// <summary>Invalid invocation resource mapping value.</summary>
    public const string InvalidInvocationResourceMappingValue = "SKW3109";

    /// <summary>Malformed locator reference wrapper.</summary>
    public const string MalformedLocatorReferenceWrapper = "SKW3110";

    /// <summary>Invalid locator catalog ID.</summary>
    public const string InvalidLocatorCatalogId = "SKW3111";

    /// <summary>Invalid locator ID.</summary>
    public const string InvalidLocatorId = "SKW3112";

    /// <summary>Invalid locator version.</summary>
    public const string InvalidLocatorVersion = "SKW3113";

    /// <summary>Unsupported interaction node version.</summary>
    public const string UnsupportedInteractionNodeVersion = "SKW3114";

    /// <summary>Invalid interaction kind.</summary>
    public const string InvalidInteractionKind = "SKW3115";

    /// <summary>Invalid interaction prompt.</summary>
    public const string InvalidInteractionPrompt = "SKW3116";

    /// <summary>Invalid interaction options.</summary>
    public const string InvalidInteractionOptions = "SKW3117";

    /// <summary>Duplicate interaction option ID.</summary>
    public const string DuplicateInteractionOptionId = "SKW3118";

    /// <summary>Invalid interaction default.</summary>
    public const string InvalidInteractionDefault = "SKW3119";

    /// <summary>Invalid interaction timeout.</summary>
    public const string InvalidInteractionTimeout = "SKW3120";

    /// <summary>Invalid interaction port.</summary>
    public const string InvalidInteractionPort = "SKW3121";

    /// <summary>Secret interaction contains a prohibited default.</summary>
    public const string SecretInteractionContainsProhibitedDefault = "SKW3122";
}
