namespace SkeletonKey.Runner.Core.Plugins;

/// <summary>Represents a stable failure while validating or loading an explicit local plugin package.</summary>
public sealed class SkeletonKeyPluginLoadException : Exception
{
    /// <summary>Initializes a plugin loading failure.</summary>
    public SkeletonKeyPluginLoadException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Initializes a plugin loading failure with an underlying exception.</summary>
    public SkeletonKeyPluginLoadException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Gets the stable plugin loading error code.</summary>
    public string Code { get; }
}
