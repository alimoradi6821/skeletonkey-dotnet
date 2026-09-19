using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using SkeletonKey.State.Abstractions;

namespace SkeletonKey.State.Sqlite;

/// <summary>Implements durable cross-execution workflow state with a local SQLite database.</summary>
public sealed class SqliteWorkflowStateStore : IWorkflowStateStore, IWorkflowLeaseStore, IDisposable
{
    private const int _schemaVersion = 2;
    private readonly string _connectionString;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a local durable state store.</summary>
    public SqliteWorkflowStateStore(string databasePath, int busyTimeoutSeconds = 5, TimeProvider? timeProvider = null)
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
        _timeProvider = timeProvider ?? TimeProvider.System;
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
    public async ValueTask<WorkflowLeaseAcquireResult> TryAcquireLeaseAsync(
        WorkflowStateAddress address,
        string ownerId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ValidateLeaseDuration(duration);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expires = now.Add(duration);
        string leaseId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        try
        {
            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO skeletonkey_state_leases(
                    scope, namespace, key, lease_id, owner_id, fencing_token, acquired_unix_ms, expires_unix_ms, updated_unix_ms)
                VALUES ($scope, $namespace, $key, $leaseId, $ownerId, 1, $now, $expires, $now)
                ON CONFLICT(scope, namespace, key) DO UPDATE SET
                    lease_id = excluded.lease_id,
                    owner_id = excluded.owner_id,
                    fencing_token = skeletonkey_state_leases.fencing_token + 1,
                    acquired_unix_ms = excluded.acquired_unix_ms,
                    expires_unix_ms = excluded.expires_unix_ms,
                    updated_unix_ms = excluded.updated_unix_ms
                WHERE skeletonkey_state_leases.lease_id IS NULL
                   OR skeletonkey_state_leases.expires_unix_ms <= $now;
                """;
            BindAddress(command, address);
            command.Parameters.AddWithValue("$leaseId", leaseId);
            command.Parameters.AddWithValue("$ownerId", ownerId);
            command.Parameters.AddWithValue("$now", now.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$expires", expires.ToUnixTimeMilliseconds());
            int changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            WorkflowLease? current = await ReadActiveLeaseAsync(connection, address, now, cancellationToken).ConfigureAwait(false);
            return changed == 1
                ? new WorkflowLeaseAcquireResult(true, current ?? throw new InvalidOperationException("Acquired lease could not be read back."), current)
                : new WorkflowLeaseAcquireResult(false, null, current);
        }
        catch (SqliteException exception)
        {
            throw Wrap(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowLeaseMutationResult> RenewLeaseAsync(
        WorkflowStateAddress address,
        string leaseId,
        long fencingToken,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);
        if (fencingToken < 1)
        {
            throw new WorkflowStateStoreException(WorkflowStateErrorCodes.InvalidLeaseRequest, "Lease fencing token must be positive.");
        }

        ValidateLeaseDuration(duration);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expires = now.Add(duration);
        try
        {
            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE skeletonkey_state_leases
                SET expires_unix_ms = $expires, updated_unix_ms = $now
                WHERE scope = $scope AND namespace = $namespace AND key = $key
                  AND lease_id = $leaseId AND fencing_token = $fencingToken
                  AND expires_unix_ms > $now;
                """;
            BindAddress(command, address);
            command.Parameters.AddWithValue("$leaseId", leaseId);
            command.Parameters.AddWithValue("$fencingToken", fencingToken);
            command.Parameters.AddWithValue("$now", now.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$expires", expires.ToUnixTimeMilliseconds());
            int changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            WorkflowLease? current = await ReadActiveLeaseAsync(connection, address, now, cancellationToken).ConfigureAwait(false);
            return new WorkflowLeaseMutationResult(changed == 1, current);
        }
        catch (SqliteException exception)
        {
            throw Wrap(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkflowLeaseMutationResult> ReleaseLeaseAsync(
        WorkflowStateAddress address,
        string leaseId,
        long fencingToken,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);
        if (fencingToken < 1)
        {
            throw new WorkflowStateStoreException(WorkflowStateErrorCodes.InvalidLeaseRequest, "Lease fencing token must be positive.");
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        try
        {
            await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE skeletonkey_state_leases
                SET lease_id = NULL,
                    owner_id = NULL,
                    acquired_unix_ms = NULL,
                    expires_unix_ms = NULL,
                    updated_unix_ms = $now
                WHERE scope = $scope AND namespace = $namespace AND key = $key
                  AND lease_id = $leaseId AND fencing_token = $fencingToken
                  AND expires_unix_ms > $now;
                """;
            BindAddress(command, address);
            command.Parameters.AddWithValue("$leaseId", leaseId);
            command.Parameters.AddWithValue("$fencingToken", fencingToken);
            command.Parameters.AddWithValue("$now", now.ToUnixTimeMilliseconds());
            int changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            WorkflowLease? current = await ReadActiveLeaseAsync(connection, address, now, cancellationToken).ConfigureAwait(false);
            return new WorkflowLeaseMutationResult(changed == 1, current);
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
            if (version > _schemaVersion)
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
                CREATE TABLE IF NOT EXISTS skeletonkey_state_leases(
                    scope INTEGER NOT NULL,
                    namespace TEXT NOT NULL,
                    key TEXT NOT NULL,
                    lease_id TEXT NULL,
                    owner_id TEXT NULL,
                    fencing_token INTEGER NOT NULL,
                    acquired_unix_ms INTEGER NULL,
                    expires_unix_ms INTEGER NULL,
                    updated_unix_ms INTEGER NOT NULL,
                    PRIMARY KEY(scope, namespace, key)
                );
                PRAGMA user_version = 2;
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

    private static void ValidateLeaseDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(30))
        {
            throw new WorkflowStateStoreException(WorkflowStateErrorCodes.InvalidLeaseRequest, "Lease duration must be greater than zero and no more than 30 days.");
        }
    }

    private static async ValueTask<WorkflowLease?> ReadActiveLeaseAsync(
        SqliteConnection connection,
        WorkflowStateAddress address,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT lease_id, owner_id, fencing_token, acquired_unix_ms, expires_unix_ms
            FROM skeletonkey_state_leases
            WHERE scope = $scope AND namespace = $namespace AND key = $key
              AND lease_id IS NOT NULL AND expires_unix_ms > $now;
            """;
        BindAddress(command, address);
        command.Parameters.AddWithValue("$now", now.ToUnixTimeMilliseconds());
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new WorkflowLease(
            address,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)));
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
