using SkeletonKey.Artifacts;

namespace SkeletonKey.Artifacts.Tests;

/// <summary>
/// Covers artifact contract immutability and sensitivity metadata.
/// </summary>
public sealed class WorkflowArtifactContractTests
{
    /// <summary>
    /// Verifies artifact references expose metadata without host paths.
    /// </summary>
    [Fact]
    public void ReferenceCarriesMetadataWithoutFilePath()
    {
        WorkflowArtifactReference reference = new("artifact-1", "report.txt", "text/plain", 12, WorkflowArtifactSensitivity.Sensitive, "abc");

        Assert.Equal("artifact-1", reference.ArtifactId);
        Assert.Equal("report.txt", reference.Filename);
        Assert.Equal("text/plain", reference.MediaType);
        Assert.Equal(12, reference.Size);
        Assert.Equal(WorkflowArtifactSensitivity.Sensitive, reference.Sensitivity);
        Assert.Equal("abc", reference.Sha256);
    }
}
