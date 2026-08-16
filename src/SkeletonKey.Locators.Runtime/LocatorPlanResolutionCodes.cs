namespace SkeletonKey.Locators.Runtime;

/// <summary>
/// Defines stable locator plan resolution error codes.
/// </summary>
public static class LocatorPlanResolutionCodes
{
    /// <summary>The exact locator document was not found.</summary>
    public const string DocumentNotFound = "SKR2005";

    /// <summary>The locator ID was not found in the document.</summary>
    public const string LocatorNotFound = "SKR2006";

    /// <summary>The locator document version is unsupported.</summary>
    public const string UnsupportedVersion = "SKR2006";

    /// <summary>The locator scope graph contains a cycle.</summary>
    public const string ScopeCycle = "SKR2009";
}
