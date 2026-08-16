using SkeletonKey.Handlers;

namespace SkeletonKey.Web.BuiltIns;

/// <summary>
/// Creates immutable handler collections for essential web automation nodes.
/// </summary>
public static class WebBuiltInRuntimeHandlers
{
    /// <summary>Creates the essential web automation handler set.</summary>
    public static IReadOnlyList<INodeHandler> Create()
    {
        return Array.AsReadOnly<INodeHandler>(
        [
            new WebNavigateHandler(),
            new WebClickHandler(),
            new WebFillHandler(),
            new WebPressHandler(),
            new WebSelectOptionHandler(),
            new WebSetCheckedHandler(),
            new WebWaitHandler(),
            new WebGetTextHandler(),
            new WebGetAttributeHandler(),
            new WebGetCountHandler(),
            new WebScreenshotHandler(),
            new WebOpenPageHandler(),
            new WebListPagesHandler(),
            new WebActivatePageHandler(),
            new WebClosePageHandler(),
            new WebClickAndWaitForPopupHandler(),
            new WebUploadFilesHandler(),
            new WebClickAndWaitForDownloadHandler(),
            new WebClickAndWaitForDialogHandler(),
            new WebRespondDialogHandler(),
            new WebGetCookiesHandler(),
            new WebSetCookiesHandler(),
            new WebClearCookiesHandler(),
            new WebExportStorageStateHandler(),
            new WebImportStorageStateHandler(),
            new WebWaitForUrlHandler(),
            new WebWaitForLoadStateHandler(),
        ]);
    }
}
