namespace SkeletonKey.Evaluation;

/// <summary>
/// Defines stable workflow value processing error codes.
/// </summary>
public static class WorkflowValueErrorCode
{
    /// <summary>Malformed workflow-value wrapper.</summary>
    public const string MalformedWorkflowValueWrapper = "SKV1001";

    /// <summary>Unknown input.</summary>
    public const string UnknownInput = "SKV1002";

    /// <summary>Unknown variable.</summary>
    public const string UnknownVariable = "SKV1003";

    /// <summary>Unknown node.</summary>
    public const string UnknownNode = "SKV1004";

    /// <summary>Unknown node output port.</summary>
    public const string UnknownNodeOutputPort = "SKV1005";

    /// <summary>Unknown iteration.</summary>
    public const string UnknownIteration = "SKV1006";

    /// <summary>Missing binding source value.</summary>
    public const string MissingBindingSourceValue = "SKV1007";

    /// <summary>Invalid JSON Pointer.</summary>
    public const string InvalidJsonPointer = "SKV1008";

    /// <summary>JSON Pointer target not found.</summary>
    public const string JsonPointerTargetNotFound = "SKV1009";

    /// <summary>Invalid expression.</summary>
    public const string InvalidExpression = "SKV1010";

    /// <summary>Invalid expression operand type.</summary>
    public const string InvalidExpressionOperandType = "SKV1011";

    /// <summary>Invalid function argument.</summary>
    public const string InvalidFunctionArgument = "SKV1012";

    /// <summary>Division by zero.</summary>
    public const string DivisionByZero = "SKV1013";

    /// <summary>Numeric overflow.</summary>
    public const string NumericOverflow = "SKV1014";

    /// <summary>Invalid member access.</summary>
    public const string InvalidMemberAccess = "SKV1015";

    /// <summary>Invalid index access.</summary>
    public const string InvalidIndexAccess = "SKV1016";

    /// <summary>Resource reference cannot be JSON-materialized.</summary>
    public const string ResourceReferenceCannotBeJsonMaterialized = "SKV1017";

    /// <summary>Locator reference cannot be JSON-materialized.</summary>
    public const string LocatorReferenceCannotBeJsonMaterialized = "SKV1018";

    /// <summary>Evaluation operation limit exceeded.</summary>
    public const string EvaluationOperationLimitExceeded = "SKV1019";

    /// <summary>Materialization depth limit exceeded.</summary>
    public const string MaterializationDepthLimitExceeded = "SKV1020";

    /// <summary>Result size limit exceeded.</summary>
    public const string ResultSizeLimitExceeded = "SKV1021";

    /// <summary>Internal evaluation failure.</summary>
    public const string InternalEvaluationFailure = "SKV1022";
}
