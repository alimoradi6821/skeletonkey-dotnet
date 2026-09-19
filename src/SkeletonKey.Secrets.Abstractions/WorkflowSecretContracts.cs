using System.Text.Json;
using System.Text.Json.Nodes;

namespace SkeletonKey.Secrets.Abstractions;

/// <summary>Provides host-owned secret values by stable logical name.</summary>
public interface IWorkflowSecretProvider
{
    /// <summary>Resolves one secret or returns null when the name is unavailable.</summary>
    public ValueTask<WorkflowSecretValue?> ResolveAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>Wraps a sensitive value without exposing it through ordinary string formatting.</summary>
public sealed class WorkflowSecretValue
{
    private readonly string _value;

    /// <summary>Initializes a secret value.</summary>
    public WorkflowSecretValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>Gets the number of UTF-16 characters in the secret.</summary>
    public int Length => _value.Length;

    /// <summary>Reveals the value to the runtime boundary that explicitly requested it.</summary>
    public string Reveal() => _value;

    /// <inheritdoc />
    public override string ToString() => "[REDACTED]";
}

/// <summary>Identifies one workflow secret by logical name.</summary>
public sealed record WorkflowSecretReference
{
    /// <summary>Initializes a secret reference.</summary>
    public WorkflowSecretReference(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Secret names cannot exceed 256 characters.");
        }

        Name = name;
    }

    /// <summary>Gets the host-resolved secret name.</summary>
    public string Name { get; }
}

/// <summary>Reports an invalid <c>$secret</c> wrapper.</summary>
public sealed class WorkflowSecretReferenceFormatException : Exception
{
    /// <summary>Initializes a format exception.</summary>
    public WorkflowSecretReferenceFormatException(string message, string jsonPath)
        : base(message)
    {
        JsonPath = jsonPath;
    }

    /// <summary>Gets the JSON pointer associated with the invalid value.</summary>
    public string JsonPath { get; }
}

/// <summary>Parses provider-neutral <c>$secret</c> workflow-value wrappers.</summary>
public sealed class WorkflowSecretReferenceReader
{
    /// <summary>Reads one exact wrapper of the form <c>{"$secret":"name"}</c>.</summary>
    public WorkflowSecretReference Read(JsonNode wrapper)
    {
        ArgumentNullException.ThrowIfNull(wrapper);
        if (wrapper is not JsonObject obj || obj.Count != 1 || !obj.TryGetPropertyValue("$secret", out JsonNode? node))
        {
            throw new WorkflowSecretReferenceFormatException("Secret reference wrapper must contain exactly one '$secret' property.", string.Empty);
        }

        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            throw new WorkflowSecretReferenceFormatException("Secret reference name must be a string.", "/$secret");
        }

        string name = value.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
        {
            throw new WorkflowSecretReferenceFormatException("Secret reference name must be non-empty and at most 256 characters.", "/$secret");
        }

        return new WorkflowSecretReference(name);
    }
}
