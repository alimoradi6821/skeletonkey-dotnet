using SkeletonKey.Locators;

namespace SkeletonKey.Workflow.Tests.References;

/// <summary>
/// Covers locator reference contracts used by workflow values.
/// </summary>
public sealed class LocatorReferenceContractTests
{
    /// <summary>
    /// Verifies locator references preserve catalog, version, and ID.
    /// </summary>
    [Fact]
    public void LocatorReferencesPreserveCatalogVersionAndId()
    {
        LocatorReference reference = new("bale-contacts", "add-contact-button", "1.0.0");

        Assert.Equal("bale-contacts", reference.Catalog);
        Assert.Equal("1.0.0", reference.Version);
        Assert.Equal("add-contact-button", reference.Id);
    }
}
