namespace SkeletonKey.Desktop.Abstractions;

/// <summary>Describes a desktop click request.</summary>
public sealed record DesktopClickRequest(string Button = "left", int ClickCount = 1, int? TimeoutMilliseconds = null, int? ElementIndex = null);

/// <summary>Describes a desktop text-fill request.</summary>
public sealed record DesktopFillRequest(string Value, int? TimeoutMilliseconds = null, int? ElementIndex = null);

/// <summary>Describes a desktop key-press request.</summary>
public sealed record DesktopPressRequest(string Key, int? TimeoutMilliseconds = null, int? ElementIndex = null);

/// <summary>Describes a desktop element query request.</summary>
public sealed record DesktopQueryRequest(int? TimeoutMilliseconds = null, int? ElementIndex = null);
