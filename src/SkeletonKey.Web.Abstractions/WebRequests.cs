namespace SkeletonKey.Web.Abstractions;

/// <summary>Describes a navigation request.</summary>
public sealed record WebNavigationRequest(string Url, WebNavigationWaitUntil WaitUntil = WebNavigationWaitUntil.Load, int TimeoutMilliseconds = 30000);

/// <summary>Describes a click request.</summary>
public sealed record WebClickRequest(string Button = "left", int ClickCount = 1, int TimeoutMilliseconds = 30000, int? ElementIndex = null, WebTargetContext? TargetContext = null);

/// <summary>Describes a fill request.</summary>
public sealed record WebFillRequest(string Value, int TimeoutMilliseconds = 30000, int? ElementIndex = null, WebTargetContext? TargetContext = null);

/// <summary>Describes a key press request.</summary>
public sealed record WebPressRequest(string Key, int TimeoutMilliseconds = 30000, int? ElementIndex = null, WebTargetContext? TargetContext = null);

/// <summary>Describes a select-option request.</summary>
public sealed record WebSelectOptionRequest(IReadOnlyList<string> Values, int TimeoutMilliseconds = 30000, int? ElementIndex = null, WebTargetContext? TargetContext = null);

/// <summary>Describes a set-checked request.</summary>
public sealed record WebSetCheckedRequest(bool Checked, int TimeoutMilliseconds = 30000, int? ElementIndex = null, WebTargetContext? TargetContext = null);

/// <summary>Describes a locator state wait request.</summary>
public sealed record WebWaitRequest(WebWaitState State = WebWaitState.Visible, int TimeoutMilliseconds = 30000, int? ElementIndex = null, WebTargetContext? TargetContext = null);

/// <summary>Describes a screenshot request.</summary>
public sealed record WebScreenshotRequest(WebScreenshotFormat Format = WebScreenshotFormat.Png, int TimeoutMilliseconds = 30000, int MaximumBytes = 4 * 1024 * 1024, int? ElementIndex = null, WebTargetContext? TargetContext = null);
