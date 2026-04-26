using Arrr.Core.Data.Api;
using Arrr.Core.Data.History;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Microsoft.Data.Sqlite;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Arrr.Service.Internal;

internal class NotificationHistoryService : INotificationHistoryService, IDisposable
{
    private const string SqlCreateSchema = """
                                           CREATE TABLE IF NOT EXISTS notifications (
                                               id        TEXT    NOT NULL PRIMARY KEY,
                                               source    TEXT    NOT NULL,
                                               title     TEXT    NOT NULL,
                                               body      TEXT    NOT NULL,
                                               timestamp TEXT    NOT NULL,
                                               icon_url  TEXT,
                                               priority  INTEGER NOT NULL DEFAULT 1
                                           );
                                           CREATE INDEX IF NOT EXISTS idx_notifications_timestamp ON notifications (timestamp DESC);
                                           CREATE INDEX IF NOT EXISTS idx_notifications_source    ON notifications (source);
                                           """;

    private const string SqlInsert = """
                                     INSERT INTO notifications (id, source, title, body, timestamp, icon_url, priority)
                                     VALUES (@id, @source, @title, @body, @timestamp, @icon_url, @priority)
                                     """;

    private const string SqlCount = "SELECT COUNT(*) FROM notifications";

    private const string SqlSelectPrefix = """
                                           SELECT id, source, title, body, timestamp, icon_url, priority
                                           FROM notifications
                                           """;

    private const string SqlSelectSuffix = """

                                           ORDER BY timestamp DESC
                                           LIMIT @limit OFFSET @offset
                                           """;

    private const string SqlDelete = "DELETE FROM notifications";

    private readonly ILogger _logger = Log.ForContext<NotificationHistoryService>();
    private readonly SqliteConnection _connection;

    public NotificationHistoryService(string dbPath)
    {
        var password = EncryptionUtils.DeriveRawKeyHex("arrr-history-db-v1");
        var connStr = new SqliteConnectionStringBuilder { DataSource = dbPath, Password = password }.ToString();
        _connection = OpenConnection(dbPath, connStr);
        EnsureSchema();
    }

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = SqlInsert;
        cmd.Parameters.AddWithValue("@id", notification.Id.ToString());
        cmd.Parameters.AddWithValue("@source", notification.Source);
        cmd.Parameters.AddWithValue("@title", notification.Title);
        cmd.Parameters.AddWithValue("@body", notification.Body);
        cmd.Parameters.AddWithValue("@timestamp", notification.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@icon_url", (object?)notification.IconUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@priority", (int)notification.Priority);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = SqlDelete;
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.Information("Notification history cleared");
    }

    public void Dispose()
        => _connection.Dispose();

    public async Task<HistoryPageResponse> GetPageAsync(
        int page,
        int limit,
        string? search = null,
        string? source = null,
        CancellationToken ct = default
    )
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);
        var offset = (page - 1) * limit;

        var where = BuildWhere(search, source);

        await using var countCmd = _connection.CreateCommand();
        countCmd.CommandText = SqlCount + where;
        AddFilterParams(countCmd, search, source);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var queryCmd = _connection.CreateCommand();
        queryCmd.CommandText = SqlSelectPrefix + where + SqlSelectSuffix;
        AddFilterParams(queryCmd, search, source);
        queryCmd.Parameters.AddWithValue("@limit", limit);
        queryCmd.Parameters.AddWithValue("@offset", offset);

        var items = new List<NotificationHistoryEntry>();
        await using var reader = await queryCmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            items.Add(
                new(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    (NotificationPriority)reader.GetInt32(6)
                )
            );
        }

        return new(items, total, page, limit);
    }

    private static void AddFilterParams(SqliteCommand cmd, string? search, string? source)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            cmd.Parameters.AddWithValue("@source", source);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            cmd.Parameters.AddWithValue("@search", $"%{search}%");
        }
    }

    private static string BuildWhere(string? search, string? source)
    {
        var clauses = new List<string>();

        if (!string.IsNullOrWhiteSpace(source))
        {
            clauses.Add("source = @source");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            clauses.Add("(title LIKE @search OR body LIKE @search)");
        }

        return clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : "";
    }

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = SqlCreateSchema;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection(string dbPath, string connStr)
    {
        var conn = new SqliteConnection(connStr);

        try
        {
            conn.Open();

            return conn;
        }
        catch (SqliteException ex)
        {
            // Existing DB is unencrypted or from a different machine — discard and start fresh.
            conn.Dispose();
            SqliteConnection.ClearAllPools(); // release pooled broken connection before retrying
            _logger.Warning(ex, "Could not open history DB with encryption key, recreating at {Path}", dbPath);

            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
            var fresh = new SqliteConnection(connStr);
            fresh.Open();

            return fresh;
        }
    }
}
