using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace AppointmentSystem.Data.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly DbConnectionFactory _factory;
    public NotificationRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<Notification>> GetForUserAsync(int userId, int take = 50)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT TOP (@N) Id, UserId, Title, Body, RelatedAppointmentId, IsRead, CreatedAt
            FROM Notifications
            WHERE UserId = @U
            ORDER BY CreatedAt DESC", conn);
        cmd.Parameters.AddWithValue("@U", userId);
        cmd.Parameters.AddWithValue("@N", take);
        var list = new List<Notification>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Notification
            {
                Id = r.GetInt32(0), UserId = r.GetInt32(1),
                Title = r.GetString(2), Body = r.GetString(3),
                RelatedAppointmentId = r.IsDBNull(4) ? null : r.GetInt32(4),
                IsRead = r.GetBoolean(5), CreatedAt = r.GetDateTime(6)
            });
        return list;
    }

    public async Task<int> UnreadCountAsync(int userId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM Notifications WHERE UserId = @U AND IsRead = 0", conn);
        cmd.Parameters.AddWithValue("@U", userId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task CreateAsync(Notification n)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            INSERT INTO Notifications (UserId, Title, Body, RelatedAppointmentId)
            VALUES (@U, @T, @B, @R)", conn);
        cmd.Parameters.AddWithValue("@U", n.UserId);
        cmd.Parameters.AddWithValue("@T", n.Title);
        cmd.Parameters.AddWithValue("@B", n.Body);
        cmd.Parameters.AddWithValue("@R", (object?)n.RelatedAppointmentId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkAllReadAsync(int userId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("UPDATE Notifications SET IsRead = 1 WHERE UserId = @U", conn);
        cmd.Parameters.AddWithValue("@U", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}
