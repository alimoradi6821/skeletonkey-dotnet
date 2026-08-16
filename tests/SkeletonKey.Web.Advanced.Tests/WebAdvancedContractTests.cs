using SkeletonKey.Artifacts;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Advanced.Tests;

/// <summary>
/// Covers provider-neutral advanced web automation contracts.
/// </summary>
public sealed class WebAdvancedContractTests
{
    /// <summary>
    /// Verifies page, upload, and storage-state contracts preserve opaque references.
    /// </summary>
    [Fact]
    public void AdvancedContractsCarryOpaqueReferences()
    {
        WebPageReference page = new("page-2");
        WorkflowArtifactReference artifact = new("artifact-1", "state.json", "application/json", 10, WorkflowArtifactSensitivity.Sensitive);
        WebUploadFilesRequest upload = new(new WebTargetContext(page), [artifact]);
        WebStorageStateRequest storage = new(2048);

        Assert.Equal("page-2", upload.TargetContext.Page!.PageId);
        Assert.Equal("artifact-1", upload.Artifacts[0].ArtifactId);
        Assert.Equal(16, upload.MaximumFiles);
        Assert.Equal(64 * 1024 * 1024, upload.MaximumAggregateBytes);
        Assert.Equal(2048, storage.MaximumBytes);
    }

    /// <summary>
    /// Verifies upload contracts carry explicit file-count and aggregate-size limits.
    /// </summary>
    [Fact]
    public void UploadContractsCarryLimits()
    {
        WebUploadFilesRequest upload = new(new WebTargetContext(), [], MaximumFiles: 2, MaximumAggregateBytes: 4096);

        Assert.Equal(2, upload.MaximumFiles);
        Assert.Equal(4096, upload.MaximumAggregateBytes);
    }

    /// <summary>
    /// Verifies the advanced web error map exposes stable Step 0-16 codes.
    /// </summary>
    [Fact]
    public void AdvancedErrorCodesAreStable()
    {
        Assert.Equal("SKR2020", WebAutomationErrorCodes.UnknownPageReference);
        Assert.Equal("SKR2021", WebAutomationErrorCodes.PageAlreadyClosed);
        Assert.Equal("SKR2022", WebAutomationErrorCodes.PopupTimeout);
        Assert.Equal("SKR2023", WebAutomationErrorCodes.FrameNotFound);
        Assert.Equal("SKR2024", WebAutomationErrorCodes.FrameCardinalityMismatch);
        Assert.Equal("SKR2025", WebAutomationErrorCodes.ArtifactUnavailable);
        Assert.Equal("SKR2026", WebAutomationErrorCodes.UploadFailed);
        Assert.Equal("SKR2027", WebAutomationErrorCodes.DownloadTimeout);
        Assert.Equal("SKR2028", WebAutomationErrorCodes.DownloadSizeLimitExceeded);
        Assert.Equal("SKR2029", WebAutomationErrorCodes.DownloadPersistenceFailed);
        Assert.Equal("SKR2030", WebAutomationErrorCodes.DialogTimeout);
        Assert.Equal("SKR2031", WebAutomationErrorCodes.UnknownDialogReference);
        Assert.Equal("SKR2032", WebAutomationErrorCodes.InvalidDialogResponse);
        Assert.Equal("SKR2033", WebAutomationErrorCodes.CookieOperationFailed);
        Assert.Equal("SKR2034", WebAutomationErrorCodes.StorageStateImportFailed);
        Assert.Equal("SKR2035", WebAutomationErrorCodes.StorageStateExportFailed);
        Assert.Equal("SKR2036", WebAutomationErrorCodes.StaleBrowsingContextReference);
        Assert.Equal("SKR2037", WebAutomationErrorCodes.BrowserInstallationMissing);
    }
}
