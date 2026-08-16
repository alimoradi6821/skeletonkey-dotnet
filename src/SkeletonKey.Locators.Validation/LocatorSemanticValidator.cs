using System.Globalization;
using System.Text.RegularExpressions;

namespace SkeletonKey.Locators.Validation;

/// <summary>
/// Performs deterministic semantic validation for locator document version 0.1 contracts.
/// </summary>
/// <remarks>
/// The validator is stateless, does not mutate locator documents, does not parse selectors, and does
/// not access browsers, locator catalogs, providers, or host services.
/// </remarks>
public sealed partial class LocatorSemanticValidator
{
    /// <summary>
    /// Validates a locator document.
    /// </summary>
    /// <param name="document">The locator document to validate.</param>
    /// <returns>The deterministic validation result.</returns>
    public LocatorValidationResult Validate(LocatorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<LocatorValidationIssue> issues = [];
        if (!LocatorIdRegex().IsMatch(document.Id))
        {
            Add(issues, LocatorValidationCodes.InvalidLocatorDocumentId, "Locator document ID has an invalid format.", "/id");
        }

        foreach (KeyValuePair<string, LocatorDefinition> locator in document.Locators)
        {
            string locatorPath = Combine("locators", locator.Key);
            if (!LocatorIdRegex().IsMatch(locator.Key))
            {
                Add(issues, LocatorValidationCodes.InvalidLocatorId, "Locator ID has an invalid format.", locatorPath);
            }

            ValidateDefinition(locator.Key, locator.Value, locatorPath, document.Locators, issues);
        }

        ValidateScopeCycles(document.Locators, issues);
        return new LocatorValidationResult(issues);
    }

    /// <summary>
    /// Validates a locator reference without resolving a catalog.
    /// </summary>
    /// <param name="reference">The locator reference.</param>
    /// <returns>The deterministic validation result.</returns>
    public LocatorValidationResult ValidateReference(LocatorReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        List<LocatorValidationIssue> issues = [];
        if (!LocatorIdRegex().IsMatch(reference.Catalog))
        {
            Add(issues, LocatorValidationCodes.InvalidLocatorDocumentId, "Locator catalog ID has an invalid format.", "/catalog");
        }

        if (!LocatorIdRegex().IsMatch(reference.Id))
        {
            Add(issues, LocatorValidationCodes.InvalidLocatorId, "Locator ID has an invalid format.", "/id");
        }

        if (reference.Version is not null && !SemanticVersionRegex().IsMatch(reference.Version))
        {
            Add(issues, LocatorValidationCodes.InvalidExactLocatorVersion, "Locator version must be an exact Semantic Version 2.0 value.", "/version");
        }

        return new LocatorValidationResult(issues);
    }

    private static void ValidateDefinition(
        string locatorId,
        LocatorDefinition definition,
        string locatorPath,
        IReadOnlyDictionary<string, LocatorDefinition> locators,
        List<LocatorValidationIssue> issues)
    {
        if (definition.Within is not null)
        {
            string withinPath = Combine(locatorPath, "within");
            if (!LocatorIdRegex().IsMatch(definition.Within) || !locators.ContainsKey(definition.Within))
            {
                Add(issues, LocatorValidationCodes.InvalidScopedLocatorReference, "Locator scope reference must target another local locator.", withinPath);
            }
            else if (string.Equals(definition.Within, locatorId, StringComparison.Ordinal))
            {
                Add(issues, LocatorValidationCodes.LocatorDirectlyScopesItself, "Locator must not directly scope itself.", withinPath);
            }
        }

        if (definition.Strategies.Count == 0)
        {
            Add(issues, LocatorValidationCodes.MissingLocatorStrategies, "Locator requires at least one strategy.", Combine(locatorPath, "strategies"));
            return;
        }

        HashSet<string> strategyKeys = new(StringComparer.Ordinal);
        for (int index = 0; index < definition.Strategies.Count; index++)
        {
            LocatorStrategy strategy = definition.Strategies[index];
            string strategyPath = Combine(Combine(locatorPath, "strategies"), index);
            ValidateStrategy(strategy, strategyPath, issues);
            if (!strategyKeys.Add(NormalizeStrategy(strategy)))
            {
                Add(issues, LocatorValidationCodes.DuplicateEquivalentStrategy, "Duplicate equivalent locator strategy is not allowed.", strategyPath);
            }
        }
    }

    private static void ValidateStrategy(LocatorStrategy strategy, string strategyPath, List<LocatorValidationIssue> issues)
    {
        if (strategy.Kind == "role")
        {
            if (string.IsNullOrWhiteSpace(strategy.Role))
            {
                Add(issues, LocatorValidationCodes.InvalidStrategyValue, "Role strategy requires non-empty role text.", Combine(strategyPath, "role"));
            }

            if (strategy.Name is not null && string.IsNullOrWhiteSpace(strategy.Name))
            {
                Add(issues, LocatorValidationCodes.InvalidStrategyValue, "Role strategy name must not be empty.", Combine(strategyPath, "name"));
            }
        }
        else if (strategy.Kind is "label" or "placeholder" or "text" or "test-id" or "title" or "alt-text")
        {
            if (string.IsNullOrWhiteSpace(strategy.Value))
            {
                Add(issues, LocatorValidationCodes.InvalidStrategyValue, "Semantic locator strategy value must not be empty.", Combine(strategyPath, "value"));
            }
        }
        else if (strategy.Kind == "css")
        {
            if (string.IsNullOrWhiteSpace(strategy.Selector))
            {
                Add(issues, LocatorValidationCodes.InvalidCssSelectorText, "CSS selector text must not be empty.", Combine(strategyPath, "selector"));
            }
        }
        else if (strategy.Kind == "xpath")
        {
            if (string.IsNullOrWhiteSpace(strategy.Selector))
            {
                Add(issues, LocatorValidationCodes.InvalidXPathSelectorText, "XPath selector text must not be empty.", Combine(strategyPath, "selector"));
            }
        }
        else
        {
            Add(issues, LocatorValidationCodes.InvalidStrategyValue, "Locator strategy kind is invalid.", Combine(strategyPath, "kind"));
        }
    }

    private static void ValidateScopeCycles(
        IReadOnlyDictionary<string, LocatorDefinition> locators,
        List<LocatorValidationIssue> issues)
    {
        foreach (string locatorId in locators.Keys)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            string current = locatorId;
            while (locators.TryGetValue(current, out LocatorDefinition? definition) && definition.Within is not null)
            {
                if (!locators.ContainsKey(definition.Within))
                {
                    break;
                }

                if (!seen.Add(current))
                {
                    break;
                }

                current = definition.Within;
                if (string.Equals(current, locatorId, StringComparison.Ordinal) && seen.Count > 1)
                {
                    Add(issues, LocatorValidationCodes.LocatorScopeCycle, "Locator scope graph contains a cycle.", Combine(Combine("locators", locatorId), "within"));
                    break;
                }
            }
        }
    }

    private static string NormalizeStrategy(LocatorStrategy strategy)
    {
        return string.Join(
            '\u001f',
            strategy.Kind,
            strategy.Role ?? string.Empty,
            strategy.Name ?? string.Empty,
            strategy.Value ?? string.Empty,
            strategy.Selector ?? string.Empty,
            strategy.Match.ToString(),
            strategy.CaseSensitive.ToString(CultureInfo.InvariantCulture));
    }

    private static void Add(List<LocatorValidationIssue> issues, string code, string message, string path)
    {
        issues.Add(new LocatorValidationIssue(code, message, path));
    }

    private static string Combine(string path, string token)
    {
        return path + "/" + token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }

    private static string Combine(string path, int index)
    {
        return path + "/" + index.ToString(CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex LocatorIdRegex();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)(?:\\.(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\\+([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionRegex();
}
