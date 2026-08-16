using System.Text.RegularExpressions;
using SkeletonKey.Abstractions.Grammar;

namespace SkeletonKey.Validation.Internal;

internal static partial class WorkflowValidationPatterns
{
    public static bool IsWorkflowOrNodeId(string value)
    {
        return WorkflowOrNodeIdRegex().IsMatch(value);
    }

    public static bool IsInputOrVariableName(string value)
    {
        return InputOrVariableNameRegex().IsMatch(value);
    }

    public static bool IsNodeType(string value)
    {
        return SkeletonKeyIdentifierGrammar.IsNodeType(value);
    }

    public static bool IsResourceName(string value)
    {
        return ResourceNameRegex().IsMatch(value);
    }

    public static bool IsDottedResourceIdentifier(string value)
    {
        return DottedResourceIdentifierRegex().IsMatch(value);
    }

    public static bool IsPortName(string value)
    {
        return PortNameRegex().IsMatch(value);
    }

    public static bool IsOutputChannelName(string value)
    {
        return OutputChannelNameRegex().IsMatch(value);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowOrNodeIdRegex();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex InputOrVariableNameRegex();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ResourceNameRegex();

    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex DottedResourceIdentifierRegex();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PortNameRegex();

    [GeneratedRegex("^[a-z][a-z0-9.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex OutputChannelNameRegex();
}
