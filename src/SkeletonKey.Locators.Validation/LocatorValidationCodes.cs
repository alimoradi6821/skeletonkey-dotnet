namespace SkeletonKey.Locators.Validation;

/// <summary>
/// Defines stable semantic validation codes for locator document version 0.1.
/// </summary>
public static class LocatorValidationCodes
{
    /// <summary>Invalid locator document ID.</summary>
    public const string InvalidLocatorDocumentId = "SKL1001";

    /// <summary>Invalid locator ID.</summary>
    public const string InvalidLocatorId = "SKL1002";

    /// <summary>Missing locator strategies.</summary>
    public const string MissingLocatorStrategies = "SKL1003";

    /// <summary>Duplicate equivalent strategy.</summary>
    public const string DuplicateEquivalentStrategy = "SKL1004";

    /// <summary>Invalid scoped locator reference.</summary>
    public const string InvalidScopedLocatorReference = "SKL1005";

    /// <summary>Locator directly scopes itself.</summary>
    public const string LocatorDirectlyScopesItself = "SKL1006";

    /// <summary>Locator scope cycle.</summary>
    public const string LocatorScopeCycle = "SKL1007";

    /// <summary>Invalid exact locator version.</summary>
    public const string InvalidExactLocatorVersion = "SKL1008";

    /// <summary>Invalid strategy value.</summary>
    public const string InvalidStrategyValue = "SKL1009";

    /// <summary>Invalid CSS selector text.</summary>
    public const string InvalidCssSelectorText = "SKL1010";

    /// <summary>Invalid XPath selector text.</summary>
    public const string InvalidXPathSelectorText = "SKL1011";
}
