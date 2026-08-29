using System.Text;
using SkeletonKey.Runner.Core;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Tests standalone package manifest identity and tamper validation.</summary>
public sealed class StandalonePackageManifestTests
{
    /// <summary>Verifies dependency enumeration order does not affect the computed package identity.</summary>
    [Fact]
    public void PackageIdentityIsStableAcrossDependencyEnumerationOrder()
    {
        StandaloneContentIdentity workflow = Identity("scenario.workflow.json", "workflow");
        StandaloneContentIdentity settings = Identity("execution.settings.json", "settings");
        StandaloneContentIdentity first = Identity("locators/a.locators.json", "a");
        StandaloneContentIdentity second = Identity("workflows/b.workflow.json", "b");

        string left = StandalonePackageManifest.ComputePackageId("0.1.0", "win-x64", workflow, settings, [first, second]);
        string right = StandalonePackageManifest.ComputePackageId("0.1.0", "win-x64", workflow, settings, [second, first]);

        Assert.Equal(left, right);
    }

    /// <summary>Verifies settings content participates in the package identity.</summary>
    [Fact]
    public void ChangingSettingsChangesPackageIdentity()
    {
        StandaloneContentIdentity workflow = Identity("scenario.workflow.json", "workflow");
        StandaloneContentIdentity settingsA = Identity("execution.settings.json", "PT5M");
        StandaloneContentIdentity settingsB = Identity("execution.settings.json", "PT10M");

        string left = StandalonePackageManifest.ComputePackageId("0.1.0", "win-x64", workflow, settingsA, []);
        string right = StandalonePackageManifest.ComputePackageId("0.1.0", "win-x64", workflow, settingsB, []);

        Assert.NotEqual(left, right);
    }

    /// <summary>Verifies deserialization rejects a manifest with a tampered package identity.</summary>
    [Fact]
    public void DeserializeRejectsTamperedPackageIdentity()
    {
        StandaloneContentIdentity workflow = Identity("scenario.workflow.json", "workflow");
        StandaloneContentIdentity settings = Identity("execution.settings.json", "settings");
        StandalonePackageManifest manifest = new(
            StandalonePackageManifest.CurrentFormat,
            "standalone:sha256:" + new string('0', 64),
            "0.1.0",
            "win-x64",
            workflow,
            settings,
            []);

        StandalonePackageException error = Assert.Throws<StandalonePackageException>(() => StandalonePackageManifest.Deserialize(manifest.Serialize()));

        Assert.Equal("SKX2006", error.Code);
    }

    private static StandaloneContentIdentity Identity(string path, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return new StandaloneContentIdentity(path, StandalonePackageManifest.ComputeSha256(bytes), bytes.LongLength);
    }
}
