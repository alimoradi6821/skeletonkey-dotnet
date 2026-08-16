using System.Globalization;
using System.Text.RegularExpressions;
using SkeletonKey.Abstractions.Grammar;

namespace SkeletonKey.Catalog.Validation;

/// <summary>
/// Performs deterministic semantic validation for node catalog documents.
/// </summary>
public sealed partial class NodeCatalogSemanticValidator
{
    /// <summary>
    /// Validates a node catalog document without resolving handlers, plugins, or runtime services.
    /// </summary>
    /// <param name="document">The catalog document.</param>
    /// <returns>The deterministic validation result.</returns>
    public NodeCatalogValidationResult Validate(NodeCatalogDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<NodeCatalogValidationIssue> issues = [];
        if (!IdentifierRegex().IsMatch(document.Id))
        {
            Add(issues, NodeCatalogValidationCodes.InvalidCatalogId, "Catalog ID has an invalid format.", "/id");
        }

        if (!SemanticVersionRegex().IsMatch(document.Version))
        {
            Add(issues, NodeCatalogValidationCodes.InvalidCatalogVersion, "Catalog version must be an exact Semantic Version 2.0 value.", "/version");
        }

        if (document.Definitions.Count == 0)
        {
            Add(issues, NodeCatalogValidationCodes.MissingDefinitions, "Catalog must declare at least one node definition.", "/definitions");
        }

        HashSet<WorkflowNodeDefinitionKey> seenDefinitions = [];
        for (int index = 0; index < document.Definitions.Count; index++)
        {
            WorkflowNodeDefinition definition = document.Definitions[index];
            string definitionPath = Combine("definitions", index);
            ValidateDefinition(definition, definitionPath, issues);
            if (!seenDefinitions.Add(definition.Key))
            {
                Add(issues, NodeCatalogValidationCodes.DuplicateNodeDefinition, "Duplicate node type and version definitions are not allowed.", definitionPath);
            }
        }

        return new NodeCatalogValidationResult(issues);
    }

    private static void ValidateDefinition(WorkflowNodeDefinition definition, string path, List<NodeCatalogValidationIssue> issues)
    {
        if (!SkeletonKeyIdentifierGrammar.IsNodeType(definition.Type))
        {
            Add(issues, NodeCatalogValidationCodes.InvalidNodeType, "Node type has an invalid format.", Combine(path, "type"));
        }

        if (definition.Version < 1)
        {
            Add(issues, NodeCatalogValidationCodes.InvalidNodeVersion, "Node type version must be at least 1.", Combine(path, "typeVersion"));
        }

        ValidatePorts(definition.Inputs, WorkflowPortDirection.Input, Combine(path, "inputs"), issues);
        ValidatePorts(definition.Outputs, WorkflowPortDirection.Output, Combine(path, "outputs"), issues);
        ValidateCapabilities(definition.Capabilities, Combine(path, "capabilities"), issues);
        ValidateResources(definition.Resources, Combine(path, "resources"), issues);
        ValidateLocators(definition.Locators, Combine(path, "locators"), issues);
        ValidateDynamicPorts(definition.DynamicPorts, Combine(path, "dynamicPorts"), issues);
        ValidateDeprecation(definition.Deprecation, Combine(path, "deprecation"), issues);
    }

    private static void ValidatePorts(IReadOnlyDictionary<string, WorkflowPortDefinition> ports, WorkflowPortDirection expectedDirection, string path, List<NodeCatalogValidationIssue> issues)
    {
        foreach (KeyValuePair<string, WorkflowPortDefinition> port in ports)
        {
            string portPath = Combine(path, port.Key);
            if (!PortNameRegex().IsMatch(port.Key) || !string.Equals(port.Key, port.Value.Name, StringComparison.Ordinal))
            {
                Add(issues, NodeCatalogValidationCodes.InvalidPortName, "Port name has an invalid format or does not match its dictionary key.", portPath);
            }

            if (port.Value.Direction != expectedDirection)
            {
                Add(issues, NodeCatalogValidationCodes.InvalidPortDirection, "Port direction must match the containing port collection.", Combine(portPath, "direction"));
            }
        }
    }

    private static void ValidateCapabilities(IReadOnlyList<string> capabilities, string path, List<NodeCatalogValidationIssue> issues)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 0; index < capabilities.Count; index++)
        {
            string capability = capabilities[index];
            string capabilityPath = Combine(path, index);
            if (!DottedIdRegex().IsMatch(capability))
            {
                Add(issues, NodeCatalogValidationCodes.InvalidCapabilityId, "Capability ID has an invalid format.", capabilityPath);
            }
            else if (!seen.Add(capability))
            {
                Add(issues, NodeCatalogValidationCodes.DuplicateCapability, "Duplicate capabilities are not allowed.", capabilityPath);
            }
        }
    }

    private static void ValidateResources(IReadOnlyDictionary<string, WorkflowNodeResourceRequirement> resources, string path, List<NodeCatalogValidationIssue> issues)
    {
        foreach (KeyValuePair<string, WorkflowNodeResourceRequirement> resource in resources)
        {
            string resourcePath = Combine(path, resource.Key);
            if (!IdentifierRegex().IsMatch(resource.Key) || !string.Equals(resource.Key, resource.Value.Name, StringComparison.Ordinal))
            {
                Add(issues, NodeCatalogValidationCodes.InvalidResourceSlot, "Resource slot name has an invalid format or does not match its dictionary key.", resourcePath);
            }

            if (!DottedIdRegex().IsMatch(resource.Value.Kind))
            {
                Add(issues, NodeCatalogValidationCodes.InvalidResourceKind, "Resource kind has an invalid format.", Combine(resourcePath, "kind"));
            }

            ValidateCapabilities(resource.Value.Capabilities, Combine(resourcePath, "capabilities"), issues);
        }
    }

    private static void ValidateLocators(IReadOnlyDictionary<string, NodeLocatorSlotDefinition> locators, string path, List<NodeCatalogValidationIssue> issues)
    {
        HashSet<string> pointers = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, NodeLocatorSlotDefinition> locator in locators)
        {
            string locatorPath = Combine(path, locator.Key);
            if (!IdentifierRegex().IsMatch(locator.Key) || !string.Equals(locator.Key, locator.Value.Name, StringComparison.Ordinal))
            {
                Add(issues, NodeCatalogValidationCodes.InvalidLocatorSlot, "Locator slot name has an invalid format or does not match its dictionary key.", locatorPath);
            }

            if (!IsObjectPropertyPointer(locator.Value.ParameterPointer))
            {
                Add(issues, NodeCatalogValidationCodes.InvalidLocatorSlot, "Locator slot parameter pointer must be an RFC 6901 object-property pointer.", Combine(locatorPath, "parameterPointer"));
            }
            else if (!pointers.Add(locator.Value.ParameterPointer))
            {
                Add(issues, NodeCatalogValidationCodes.InvalidLocatorSlot, "Duplicate locator slot parameter pointers are not allowed.", Combine(locatorPath, "parameterPointer"));
            }

            if (locator.Value.AcceptedCardinalities.Count == 0)
            {
                Add(issues, NodeCatalogValidationCodes.InvalidLocatorSlot, "Locator slot must declare at least one accepted cardinality.", Combine(locatorPath, "acceptedCardinalities"));
            }
        }
    }

    private static void ValidateDynamicPorts(IReadOnlyList<WorkflowDynamicPortRule> rules, string path, List<NodeCatalogValidationIssue> issues)
    {
        for (int index = 0; index < rules.Count; index++)
        {
            WorkflowDynamicPortRule rule = rules[index];
            string rulePath = Combine(path, index);
            if (rule.Kind == WorkflowDynamicPortRuleKind.SwitchCases &&
                (rule.Direction != WorkflowPortDirection.Output ||
                !string.Equals(rule.SourcePointer, "/cases", StringComparison.Ordinal) ||
                !string.Equals(rule.IdPointer, "/id", StringComparison.Ordinal)))
            {
                Add(issues, NodeCatalogValidationCodes.InvalidDynamicPortRule, "Switch dynamic port rules must derive output ports from /cases item /id values.", rulePath);
            }
        }
    }

    private static void ValidateDeprecation(WorkflowNodeDeprecationMetadata deprecation, string path, List<NodeCatalogValidationIssue> issues)
    {
        if (!deprecation.Deprecated &&
            (deprecation.SinceVersion is not null ||
            deprecation.Message is not null ||
            deprecation.ReplacementType is not null ||
            deprecation.ReplacementVersion is not null))
        {
            Add(issues, NodeCatalogValidationCodes.InvalidDeprecation, "Deprecation details require deprecated=true.", path);
        }

        if (deprecation.ReplacementType is not null && !SkeletonKeyIdentifierGrammar.IsNodeType(deprecation.ReplacementType))
        {
            Add(issues, NodeCatalogValidationCodes.InvalidDeprecation, "Replacement type has an invalid format.", Combine(path, "replacementType"));
        }

        if (deprecation.ReplacementVersion is not null && deprecation.ReplacementVersion.Value < 1)
        {
            Add(issues, NodeCatalogValidationCodes.InvalidDeprecation, "Replacement version must be at least 1.", Combine(path, "replacementVersion"));
        }
    }

    private static void Add(List<NodeCatalogValidationIssue> issues, string code, string message, string path)
    {
        issues.Add(new NodeCatalogValidationIssue(code, message, path));
    }

    private static string Combine(string path, string token)
    {
        string prefix = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        return prefix + "/" + token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }

    private static string Combine(string path, int index)
    {
        string prefix = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        return prefix + "/" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsObjectPropertyPointer(string pointer)
    {
        if (string.IsNullOrWhiteSpace(pointer) || !pointer.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = pointer[1..].Split('/');
        return segments.All(static segment => segment.Length > 0 && !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out _));
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PortNameRegex();

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex DottedIdRegex();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)(?:\\.(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\\+([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionRegex();
}
