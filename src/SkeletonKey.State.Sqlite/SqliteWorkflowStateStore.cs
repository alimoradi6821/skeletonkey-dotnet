using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using SkeletonKey.State.Abstractions;

namespace SkeletonKey.State.Sqlite;

/// <summary>Implements durable cross-execution workflow state with a local SQLite database.</summary>
public sealed class SqliteWorkflowStateStore : IWorkflowStateStore, IDisposable
{
    private const int SchemaVersion = 1;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a local durable state store.</summary>
    public SqliteWorkflowStateStore(string databasePath, int busyTimeoutSeconds = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (busyTimeoutSeconds is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(busyTimeoutSeconds));
        }

        string fullPath = Path.GetFullPath(databasePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = busyTimeoutSeconds,
        };
        _connectionString = builder.ToString();
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowStateEntry?> GetAsync(WorkflowStateAddress address, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(address);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await ReadEntryAsync(connection, address, cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw Wrap(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowStateEntry> PutAsync(WorkflowStateAddress address, JsonNode? value, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(address);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        WorkflowStateEntry entry = NewEntry(value);
        try
        {
            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO skeletonkey_state_entries(scope, namespace, key, value_json, version, updated_utc)
                VALUES ($scope, $namespace, $key, $value, $version, $updated)
                ON CONFLICT(scope, namespace, key) DO UPDATE SET
                    value_json = excluded.value_json,
                    version = excluded.version,
                    updated_utc = excluded.updated_utc;
                """;
            BindAddress(command, address);
            BindEntry(command, entry);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return entry;
        }
        catch (SqliteException exception)
        {
            throw Wrap(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(WorkflowStateAddress address, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(address);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM skeletonkey_state_entries WHERE scope = $scope AND namespace = $namespace AND key = $key;";
            BindAddress(command, address);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }
        catch (SqliteException exception)
        {
            throw Wrap(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowStateCompareExchangeResult> CompareExchangeAsync(
        WorkflowStateAddress address,
        string? expectedVersion,
        JsonNode? value,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(address);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        WorkflowStateEntry replacement = NewEntry(value);
        try
        {
            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            if (expectedVersion is null)
            {
                command.CommandText = """
                    INSERT INTO skeletonkey_state_entries(scope, namespace, key, value_json, version, updated_utc)
                    VALUES ($scope, $namespace, $key, $value, $version, $updated)
                    ON CONFLICT(scope, namespace, key) DO NOTHING;
                    """;
            }
            else
            {
                command.CommandText = """
                    UPDATE skeletonkey_state_entries
                    SET value_json = $value, version = $version, updated_utc = $updated
                    WHERE scope = $scope AND namespace = $namespace AND key = $key AND version = $expectedVersion;
                    """;
                command.Parameters.AddWithValue("$expectedVersion", expectedVersion);
            }

            BindAddress(command, address);
            BindEntry(command, replacement);
            int changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (changed == 1)
            {
                return new WorkflowStateCompareExchangeResult(true, replacement);
            }

            WorkflowStateEntry? observed = await ReadEntryAsync(connection, address, cancellationToken).ConfigureAwait(false);
            return new WorkflowStateCompareExchangeResult(false, observed);
        }
        catch (SqliteException exception)
        {
            throw Wrap(exception);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _initializationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version;";
            object? result = await versionCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            int version = Convert.ToInt32(result, CultureInfo.InvariantCulture);
            if (version > SchemaVersion)
            {
                throw new WorkflowStateStoreException(WorkflowStateErrorCodes.UnsupportedSchema, "Durable state database schema is newer than this SkeletonKey provider supports.");
            }

            await using SqliteCommand schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText = """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS skeletonkey_state_entries(
                    scope INTEGER NOT NULL,
                    namespace TEXT NOT NULL,
                    key TEXT NOT NULL,
                    value_json TEXT NOT NULL,
                    version TEXT NOT NULL,
                    updated_utc TEXT NOT NULL,
                    PRIMARY KEY(scope, namespace, key)
                );
                PRAGMA user_version = 1;
                """;
            await schemaCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        catch (SqliteException exception)
        {
            throw Wrap(exception);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private async ValueTask<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask<WorkflowStateEntry?> ReadEntryAsync(
        SqliteConnection connection,
        WorkflowStateAddress address,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value_json, version, updated_utc FROM skeletonkey_state_entries WHERE scope = $scope AND namespace = $namespace AND key = $key;";
        BindAddress(command, address);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        string json = reader.GetString(0);
        string version = reader.GetString(1);
        var updated = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new WorkflowStateEntry(JsonNode.Parse(json), version, updated);
    }

    private static WorkflowStateEntry NewEntry(JsonNode? value)
    {
        return new WorkflowStateEntry(value, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), DateTimeOffset.UtcNow);
    }

    private static void BindAddress(SqliteCommand command, WorkflowStateAddress address)
    {
        command.Parameters.AddWithValue("$scope", (int)address.Scope);
        command.Parameters.AddWithValue("$namespace", address.Namespace);
        command.Parameters.AddWithValue("$key", address.Key);
    }

    private static void BindEntry(SqliteCommand command, WorkflowStateEntry entry)
    {
        command.Parameters.AddWithValue("$value", entry.Value?.ToJsonString() ?? "null");
        command.Parameters.AddWithValue("$version", entry.Version);
        command.Parameters.AddWithValue("$updated", entry.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static WorkflowStateStoreException Wrap(SqliteException exception)
    {
        string code = exception.SqliteErrorCode is 5 or 6 ? WorkflowStateErrorCodes.StoreBusy : WorkflowStateErrorCodes.StoreFailure;
        string message = exception.SqliteErrorCode is 5 or 6 ? "Durable state store remained busy beyond the configured bound." : "Durable state store operation failed.";
        return new WorkflowStateStoreException(code, message, exception);
    }
}
