using SkeletonKey.Locators;

namespace SkeletonKey.Desktop.Abstractions;

/// <summary>Defines provider-neutral Windows desktop automation operations.</summary>
public interface IDesktopApplicationAdapter
{
    /// <summary>Clicks one resolved desktop element.</summary>
    public ValueTask ClickAsync(ResolvedLocatorPlan locator, DesktopClickRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sets text on one resolved desktop element.</summary>
    public ValueTask FillAsync(ResolvedLocatorPlan locator, DesktopFillRequest request, CancellationToken cancellationToken = default);

    /// <summary>Focuses one resolved desktop element and presses a bounded key.</summary>
    public ValueTask PressAsync(ResolvedLocatorPlan locator, DesktopPressRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets text from resolved desktop elements in UI Automation order.</summary>
    public ValueTask<IReadOnlyList<string?>> GetTextAsync(ResolvedLocatorPlan locator, DesktopQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the number of resolved desktop elements.</summary>
    public ValueTask<int> GetCountAsync(ResolvedLocatorPlan locator, DesktopQueryRequest request, CancellationToken cancellationToken = default);
}
