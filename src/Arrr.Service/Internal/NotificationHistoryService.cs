using Arrr.Core.Data.Api;
using Arrr.Core.Data.History;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Microsoft.Data.Sqlite;
using Serilog;

namespace Arrr.Service.Internal;

internal class NotificationHistoryService : INotificationHistoryService, IDisposable
{
    private readonly Serilog.ILogger _logger = Log.ForContext<NotificationHistoryService>();
    private readonly SqliteConnection _connection;

    public NotificationHistoryService(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        EnsureSchema();
    }

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO notifications (id, source, title, body, timestamp, icon_url, priority)
            VALUES (@id, @source, @title, @body, @timestamp, @icon_url, @priority)
            """;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", notification.Id.ToString());
        cmd.Parameters.AddWithValue("@source", notification.Source);
        cmd.Parameters.AddWithValue("@title", notification.Title);
        cmd.Parameters.AddWithValue("@body", notification.Body);
        cmd.Parameters.AddWithValue("@timestamp", notification.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@icon_url", (object?)notification.IconUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@priority", (int)notification.Priority);

        await cmd.ExecuteNonQueryAsync(ct);
    }

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
        var countSql = $"SELECT COUNT(*) FROM notifications{where}";
        var querySql = $"""
            SELECT id, source, title, body, timestamp, icon_url, priority
            FROM notifications{where}
            ORDER BY timestamp DESC
            LIMIT @limit OFFSET @offset
            """;

        await using var countCmd = _connection.CreateCommand();
        countCmd.CommandText = countSql;
        AddFilterParams(countCmd, search, source);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var queryCmd = _connection.CreateCommand();
        queryCmd.CommandText = querySql;
        AddFilterParams(queryCmd, search, source);
        queryCmd.Parameters.AddWithValue("@limit", limit);
        queryCmd.Parameters.AddWithValue("@offset", offset);

        var items = new List<NotificationHistoryEntry>();
        await using var reader = await queryCmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            items.Add(
                new NotificationHistoryEntry(
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

        return new HistoryPageResponse(items, total, page, limit);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM notifications";
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.Information("Notification history cleared");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
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
        cmd.ExecuteNonQuery();
    }

    private static string BuildWhere(string? search, string? source)
    {
        var clauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(source)) clauses.Add("source = @source");
        if (!string.IsNullOrWhiteSpace(search)) clauses.Add("(title LIKE @search OR body LIKE @search)");
        return clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : "";
    }

    private static void AddFilterParams(SqliteCommand cmd, string? search, string? source)
    {
        if (!string.IsNullOrWhiteSpace(source)) cmd.Parameters.AddWithValue("@source", source);
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search}%");
    }
}
