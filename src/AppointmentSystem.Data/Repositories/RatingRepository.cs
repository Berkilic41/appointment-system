using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace AppointmentSystem.Data.Repositories;

public class RatingRepository : IRatingRepository
{
    private readonly DbConnectionFactory _factory;
    public RatingRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<Rating?> GetByAppointmentAsync(int appointmentId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT Id, AppointmentId, CustomerId, ProviderId, Stars, Comment, CreatedAt FROM Ratings WHERE AppointmentId = @A", conn);
        cmd.Parameters.AddWithValue("@A", appointmentId);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    public async Task CreateAsync(Rating rating)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            INSERT INTO Ratings (AppointmentId, CustomerId, ProviderId, Stars, Comment)
            VALUES (@A, @C, @P, @S, @Cm)", conn);
        cmd.Parameters.AddWithValue("@A", rating.AppointmentId);
        cmd.Parameters.AddWithValue("@C", rating.CustomerId);
        cmd.Parameters.AddWithValue("@P", rating.ProviderId);
        cmd.Parameters.AddWithValue("@S", (byte)rating.Stars);
        cmd.Parameters.AddWithValue("@Cm", (object?)rating.Comment ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<Rating>> GetForProviderAsync(int providerId, int take = 20)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT TOP (@N) r.Id, r.AppointmentId, r.CustomerId, r.ProviderId, r.Stars, r.Comment, r.CreatedAt,
                            c.FullName, s.Name
            FROM Ratings r
            INNER JOIN Users c ON c.Id = r.CustomerId
            INNER JOIN Appointments a ON a.Id = r.AppointmentId
            INNER JOIN Services s ON s.Id = a.ServiceId
            WHERE r.ProviderId = @P
            ORDER BY r.CreatedAt DESC", conn);
        cmd.Parameters.AddWithValue("@P", providerId);
        cmd.Parameters.AddWithValue("@N", take);
        var list = new List<Rating>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var rating = Map(r);
            rating.CustomerName = r.IsDBNull(7) ? null : r.GetString(7);
            rating.ServiceName = r.GetString(8);
            list.Add(rating);
        }
        return list;
    }

    private static Rating Map(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0), AppointmentId = r.GetInt32(1),
        CustomerId = r.GetInt32(2), ProviderId = r.GetInt32(3),
        Stars = r.GetByte(4),
        Comment = r.IsDBNull(5) ? null : r.GetString(5),
        CreatedAt = r.GetDateTime(6)
    };
}
