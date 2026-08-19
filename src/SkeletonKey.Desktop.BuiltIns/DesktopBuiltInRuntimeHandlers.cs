using SkeletonKey.Handlers;

namespace SkeletonKey.Desktop.BuiltIns;

/// <summary>Creates immutable handler collections for essential desktop automation nodes.</summary>
public static class DesktopBuiltInRuntimeHandlers
{
    /// <summary>Creates the essential desktop automation handler set.</summary>
    public static IReadOnlyList<INodeHandler> Create()
    {
        return Array.AsReadOnly<INodeHandler>(
        [
            new DesktopClickHandler(),
            new DesktopFillHandler(),
            new DesktopPressHandler(),
            new DesktopGetTextHandler(),
            new DesktopGetCountHandler(),
        ]);
    }
}
