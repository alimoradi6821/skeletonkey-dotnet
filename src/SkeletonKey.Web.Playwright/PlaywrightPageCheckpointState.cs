using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Runtime.Resources;

namespace SkeletonKey.Web.Playwright;

internal sealed class PlaywrightPageCheckpointState
{
    public const string FormatVersion = "0.1";
    public const int MaximumPages = 64;
    public const int MaximumStorageStateBytes = 4 * 1024 * 1024;
    public const int MaximumUrlLength = 8192;

    public PlaywrightPageCheckpointState(
        string storageState,
        string activePageId,
        int nextPageNumber,
        int nextDialogNumber,
        IReadOnlyList<PlaywrightCheckpointPage> pages,
        IReadOnlyList<string>? stalePageIds = null,
        IReadOnlyList<string>? staleDialogIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageState);
        ArgumentException.ThrowIfNullOrWhiteSpace(activePageId);
        if (Encoding.UTF8.GetByteCount(storageState) > MaximumStorageStateBytes || JsonNode.Parse(storageState) is not JsonObject)
        {
            throw new ArgumentException("Playwright storage state is invalid or exceeds the checkpoint limit.", nameof(storageState));
        }

        if (nextPageNumber < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(nextPageNumber));
        }

        if (nextDialogNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nextDialogNumber));
        }

        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count is < 1 or > MaximumPages || pages.Select(static page => page.Id).Distinct(StringComparer.Ordinal).Count() != pages.Count)
        {
            throw new ArgumentException("Playwright checkpoint pages are empty, duplicated, or exceed the limit.", nameof(pages));
        }

        if (!pages.Any(static page => string.Equals(page.Id, "primary", StringComparison.Ordinal)) ||
            !pages.Any(page => string.Equals(page.Id, activePageId, StringComparison.Ordinal) && !page.IsClosed))
        {
            throw new ArgumentException("Playwright checkpoint page identities are invalid.", nameof(pages));
        }

        StorageState = storageState;
        ActivePageId = activePageId;
        NextPageNumber = nextPageNumber;
        NextDialogNumber = nextDialogNumber;
        Pages = Array.AsReadOnly([.. pages]);
        StalePageIds = SnapshotIdentifiers(stalePageIds);
        StaleDialogIds = SnapshotIdentifiers(staleDialogIds);
    }

    public string StorageState { get; }

    public string ActivePageId { get; }

    public int NextPageNumber { get; }

    public int NextDialogNumber { get; }

    public IReadOnlyList<PlaywrightCheckpointPage> Pages { get; }

    public IReadOnlyList<string> StalePageIds { get; }

    public IReadOnlyList<string> StaleDialogIds { get; }

    public WorkflowRuntimeResourceCheckpointState ToResourceState()
    {
        JsonArray pages = [];
        foreach (PlaywrightCheckpointPage page in Pages)
        {
            pages.Add(new JsonObject
            {
                ["id"] = page.Id,
                ["url"] = page.Url,
                ["isClosed"] = page.IsClosed,
            });
        }

        return new WorkflowRuntimeResourceCheckpointState(
            FormatVersion,
            new JsonObject
            {
                ["storageState"] = StorageState,
                ["activePageId"] = ActivePageId,
                ["nextPageNumber"] = NextPageNumber,
                ["nextDialogNumber"] = NextDialogNumber,
                ["pages"] = pages,
                ["stalePageIds"] = new JsonArray(StalePageIds.Select(static id => (JsonNode?)JsonValue.Create(id)).ToArray()),
                ["staleDialogIds"] = new JsonArray(StaleDialogIds.Select(static id => (JsonNode?)JsonValue.Create(id)).ToArray()),
            });
    }

    public static PlaywrightPageCheckpointState Parse(WorkflowRuntimeResourceCheckpointState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!string.Equals(state.FormatVersion, FormatVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported Playwright resource checkpoint format.", nameof(state));
        }

        JsonObject payload = state.Payload;
        string[] allowed = ["storageState", "activePageId", "nextPageNumber", "nextDialogNumber", "pages", "stalePageIds", "staleDialogIds"];
        if (payload.Any(property => !allowed.Contains(property.Key, StringComparer.Ordinal)))
        {
            throw new ArgumentException("Unknown Playwright resource checkpoint property is not allowed.", nameof(state));
        }

        string storageState = RequiredString(payload, "storageState");
        string activePageId = RequiredString(payload, "activePageId");
        int nextPageNumber = RequiredInt(payload, "nextPageNumber");
        int nextDialogNumber = RequiredInt(payload, "nextDialogNumber");
        JsonArray pagesNode = payload["pages"] as JsonArray ?? throw new ArgumentException("Playwright checkpoint pages are required.", nameof(state));
        List<PlaywrightCheckpointPage> pages = [];
        foreach (JsonNode? item in pagesNode)
        {
            JsonObject page = item as JsonObject ?? throw new ArgumentException("Playwright checkpoint page entries must be objects.", nameof(state));
            if (page.Any(property => property.Key is not ("id" or "url" or "isClosed")))
            {
                throw new ArgumentException("Unknown Playwright checkpoint page property is not allowed.", nameof(state));
            }

            pages.Add(new PlaywrightCheckpointPage(RequiredString(page, "id"), RequiredString(page, "url"), RequiredBool(page, "isClosed")));
        }

        return new PlaywrightPageCheckpointState(
            storageState,
            activePageId,
            nextPageNumber,
            nextDialogNumber,
            pages,
            ReadIdentifiers(payload, "stalePageIds"),
            ReadIdentifiers(payload, "staleDialogIds"));
    }

    private static IReadOnlyList<string> SnapshotIdentifiers(IReadOnlyList<string>? values)
    {
        string[] snapshot = [.. (values ?? Array.AsReadOnly(Array.Empty<string>()))];
        if (snapshot.Length > MaximumPages || snapshot.Any(static value => string.IsNullOrWhiteSpace(value)) || snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Checkpoint reference identities are invalid or exceed the limit.", nameof(values));
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyList<string> ReadIdentifiers(JsonObject payload, string property)
    {
        JsonArray values = payload[property] as JsonArray ?? throw new ArgumentException($"Playwright checkpoint property '{property}' must be an array.", nameof(payload));
        return values.Select(value => value is not null && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new ArgumentException($"Playwright checkpoint property '{property}' contains a non-string value.", nameof(payload))).ToArray();
    }

    private static string RequiredString(JsonObject value, string property)
    {
        return value[property] is JsonNode node && node.GetValueKind() == JsonValueKind.String
            ? node.GetValue<string>()
            : throw new ArgumentException($"Playwright checkpoint property '{property}' must be a string.", nameof(value));
    }

    private static int RequiredInt(JsonObject value, string property)
    {
        return value[property] is JsonNode node && node.GetValueKind() == JsonValueKind.Number
            ? node.GetValue<int>()
            : throw new ArgumentException($"Playwright checkpoint property '{property}' must be an integer.", nameof(value));
    }

    private static bool RequiredBool(JsonObject value, string property)
    {
        return value[property] is JsonNode node && node.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? node.GetValue<bool>()
            : throw new ArgumentException($"Playwright checkpoint property '{property}' must be a boolean.", nameof(value));
    }
}

internal sealed record PlaywrightCheckpointPage
{
    public PlaywrightCheckpointPage(string id, string url, bool isClosed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(url);
        if (id.Length > 128 || url.Length > PlaywrightPageCheckpointState.MaximumUrlLength || !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Playwright checkpoint page identity or URL is invalid.");
        }

        Id = id;
        Url = url;
        IsClosed = isClosed;
    }

    public string Id { get; }

    public string Url { get; }

    public bool IsClosed { get; }
}
