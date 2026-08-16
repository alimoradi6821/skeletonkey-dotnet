using System.Globalization;
using System.Text.Json.Nodes;

namespace SkeletonKey.Evaluation;

/// <summary>
/// Resolves read-only RFC 6901 JSON Pointers without mutating source JSON.
/// </summary>
/// <remarks>
/// The resolver is stateless and thread-safe. Object property lookup is ordinal and case-sensitive. Returned JSON is defensively cloned.
/// URI fragments, append tokens, invalid tilde escapes, leading-zero array indexes, negative indexes, and scalar traversal are rejected.
/// </remarks>
public sealed class JsonPointerResolver
{
    /// <summary>
    /// Resolves a read-only JSON Pointer against a JSON source.
    /// </summary>
    /// <param name="source">The source JSON value; <see langword="null" /> is a valid explicit JSON null source.</param>
    /// <param name="pointer">The RFC 6901 pointer. The empty string selects the complete source.</param>
    /// <param name="jsonPath">The workflow JSON path associated with errors.</param>
    /// <returns>A successful result with a defensive clone, or a structured pointer error.</returns>
    public WorkflowValueResult Resolve(JsonNode? source, string pointer, string jsonPath)
    {
        if (pointer.Length == 0)
        {
            return WorkflowValueResult.Success(source);
        }

        if (pointer.StartsWith("#", StringComparison.Ordinal) || !pointer.StartsWith("/", StringComparison.Ordinal))
        {
            return Fail(WorkflowValueErrorCode.InvalidJsonPointer, "JSON Pointer must be empty or start with `/`.", jsonPath);
        }

        JsonNode? current = source;
        foreach (string rawToken in pointer[1..].Split('/'))
        {
            if (!TryDecode(rawToken, out string token))
            {
                return Fail(WorkflowValueErrorCode.InvalidJsonPointer, "JSON Pointer contains an invalid escape.", jsonPath);
            }

            if (current is JsonObject jsonObject)
            {
                if (!jsonObject.TryGetPropertyValue(token, out current))
                {
                    return Fail(WorkflowValueErrorCode.JsonPointerTargetNotFound, "JSON Pointer target was not found.", jsonPath);
                }

                continue;
            }

            if (current is JsonArray jsonArray)
            {
                if (!TryReadArrayIndex(token, jsonArray.Count, out int index))
                {
                    string code = token == "-" || token.StartsWith("-", StringComparison.Ordinal) || (token.Length > 1 && token[0] == '0')
                        ? WorkflowValueErrorCode.InvalidJsonPointer
                        : WorkflowValueErrorCode.JsonPointerTargetNotFound;
                    return Fail(code, "JSON Pointer array index is invalid or outside the array.", jsonPath);
                }

                current = jsonArray[index];
                continue;
            }

            return Fail(WorkflowValueErrorCode.JsonPointerTargetNotFound, "JSON Pointer cannot traverse through a scalar or JSON null value.", jsonPath);
        }

        return WorkflowValueResult.Success(current);
    }

    private static bool TryDecode(string token, out string decoded)
    {
        decoded = string.Empty;
        for (int index = 0; index < token.Length; index++)
        {
            char current = token[index];
            if (current != '~')
            {
                decoded += current.ToString(CultureInfo.InvariantCulture);
                continue;
            }

            if (index + 1 >= token.Length || token[index + 1] is not ('0' or '1'))
            {
                return false;
            }

            decoded += token[index + 1] == '0' ? "~" : "/";
            index++;
        }

        return true;
    }

    private static bool TryReadArrayIndex(string token, int count, out int index)
    {
        index = 0;
        if (token == "-" || token.Length == 0 || token.StartsWith("-", StringComparison.Ordinal) || (token.Length > 1 && token[0] == '0'))
        {
            return false;
        }

        foreach (char character in token)
        {
            if (!char.IsDigit(character))
            {
                return false;
            }
        }

        return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0 && index < count;
    }

    private static WorkflowValueResult Fail(string code, string message, string path)
    {
        return WorkflowValueResult.Failure(new WorkflowValueError(code, message, path));
    }
}
