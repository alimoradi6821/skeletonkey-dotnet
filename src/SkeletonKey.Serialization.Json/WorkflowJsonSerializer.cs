using System.Text;
using System.Text.Json;
using SkeletonKey.Serialization.Json.Internal;
using SkeletonKey.Workflow.Documents;

namespace SkeletonKey.Serialization.Json;

/// <summary>
/// Serializes and deserializes SkeletonKey workflow documents using strict JSON syntax rules.
/// </summary>
/// <remarks>
/// This serializer is stateless and safe for concurrent use. Deserialization checks JSON syntax only;
/// a workflow that deserializes successfully is not necessarily semantically valid.
/// </remarks>
public sealed partial class WorkflowJsonSerializer
{
    private static readonly JsonDocumentOptions _documentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    private static readonly UTF8Encoding _utf8NoBom = new(false);

    /// <summary>
    /// Deserializes strict workflow JSON into an immutable workflow document.
    /// </summary>
    /// <param name="json">The UTF-16 JSON text to parse.</param>
    /// <returns>The deserialized workflow document.</returns>
    /// <exception cref="WorkflowSerializationException">Thrown when the JSON is invalid or violates the workflow JSON shape.</exception>
    public WorkflowDocument Deserialize(string json)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(json);
            JsonDuplicatePropertyDetector.RejectDuplicates(json);

            using var document = JsonDocument.Parse(json, _documentOptions);
            return ReadWorkflowDocument(document.RootElement, string.Empty);
        }
        catch (WorkflowSerializationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw CreateJsonException(WorkflowSerializationOperation.Deserialize, exception);
        }
        catch (Exception exception)
        {
            throw new WorkflowSerializationException(
                WorkflowSerializationOperation.Deserialize,
                "Failed to deserialize workflow JSON.",
                innerException: exception);
        }
    }

    /// <summary>
    /// Serializes a workflow document into canonical workflow JSON.
    /// </summary>
    /// <param name="workflow">The workflow document to serialize.</param>
    /// <param name="indented">Whether to produce indented JSON.</param>
    /// <returns>The canonical JSON text with LF line endings and exactly one final newline.</returns>
    /// <exception cref="WorkflowSerializationException">Thrown when the document cannot be serialized.</exception>
    public string Serialize(WorkflowDocument workflow, bool indented = true)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(workflow);

            using MemoryStream stream = new();
            using Utf8JsonWriter writer = new(stream, new JsonWriterOptions
            {
                Indented = indented,
            });

            WriteWorkflowDocument(writer, workflow);
            writer.Flush();
            return NormalizeJsonText(_utf8NoBom.GetString(stream.ToArray()));
        }
        catch (WorkflowSerializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new WorkflowSerializationException(
                WorkflowSerializationOperation.Serialize,
                "Failed to serialize workflow document.",
                innerException: exception);
        }
    }

    /// <summary>
    /// Reads a UTF-8 workflow JSON file and deserializes it.
    /// </summary>
    /// <param name="path">The workflow JSON file path.</param>
    /// <param name="cancellationToken">A token used to cancel file reading.</param>
    /// <returns>The deserialized workflow document.</returns>
    /// <exception cref="WorkflowSerializationException">Thrown when the file cannot be read or the JSON cannot be deserialized.</exception>
    public async ValueTask<WorkflowDocument> DeserializeFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        string json;

        try
        {
            ValidatePath(path, WorkflowSerializationOperation.ReadFile);
            json = await File.ReadAllTextAsync(path, _utf8NoBom, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkflowSerializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new WorkflowSerializationException(
                WorkflowSerializationOperation.ReadFile,
                $"Failed to read workflow file '{path}'.",
                innerException: exception);
        }

        return Deserialize(json);
    }

    /// <summary>
    /// Serializes a workflow document and writes it as UTF-8 without BOM using a temporary file.
    /// </summary>
    /// <param name="path">The target workflow JSON file path.</param>
    /// <param name="workflow">The workflow document to write.</param>
    /// <param name="indented">Whether to produce indented JSON.</param>
    /// <param name="cancellationToken">A token used to cancel file writing before replacement.</param>
    /// <exception cref="WorkflowSerializationException">Thrown when the file cannot be written or the document cannot be serialized.</exception>
    public async ValueTask SerializeFileAsync(
        string path,
        WorkflowDocument workflow,
        bool indented = true,
        CancellationToken cancellationToken = default)
    {
        string json = Serialize(workflow, indented);
        string? temporaryPath = null;

        try
        {
            ValidatePath(path, WorkflowSerializationOperation.WriteFile);
            string fullPath = Path.GetFullPath(path);
            string? parentDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                throw new DirectoryNotFoundException($"The parent directory for '{path}' does not exist.");
            }

            temporaryPath = Path.Combine(parentDirectory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            byte[] bytes = _utf8NoBom.GetBytes(json);
            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite: true);
            temporaryPath = null;
        }
        catch (WorkflowSerializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new WorkflowSerializationException(
                WorkflowSerializationOperation.WriteFile,
                $"Failed to write workflow file '{path}'.",
                innerException: exception);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                TryDeleteFile(temporaryPath);
            }
        }
    }

    private static string NormalizeJsonText(string json)
    {
        return json.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\r', '\n') + "\n";
    }
}




