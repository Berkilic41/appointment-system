using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace AppointmentSystem.Data.Repositories;

public class ServiceRepository : IServiceRepository
{
    private readonly DbConnectionFactory _factory;
    public ServiceRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<Service>> GetAllAsync(bool includeInactive = false)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        var sql = "SELECT Id, Name, Description, DurationMinutes, Price, IsActive FROM Services"
                + (includeInactive ? "" : " WHERE IsActive = 1") + " ORDER BY Name";
        using var cmd = new SqlCommand(sql, conn);
        var list = new List<Service>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task<Service?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT Id, Name, Description, DurationMinutes, Price, IsActive FROM Services WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    public async Task<int> CreateAsync(Service s)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            INSERT INTO Services (Name, Description, DurationMinutes, Price) OUTPUT INSERTED.Id
            VALUES (@N, @D, @M, @P)", conn);
        cmd.Parameters.AddWithValue("@N", s.Name);
        cmd.Parameters.AddWithValue("@D", (object?)s.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@M", s.DurationMinutes);
        cmd.Parameters.AddWithValue("@P", s.Price);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateAsync(Service s)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            UPDATE Services SET Name=@N, Description=@D, DurationMinutes=@M, Price=@P WHERE Id=@Id", conn);
        cmd.Parameters.AddWithValue("@Id", s.Id);
        cmd.Parameters.AddWithValue("@N", s.Name);
        cmd.Parameters.AddWithValue("@D", (object?)s.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@M", s.DurationMinutes);
        cmd.Parameters.AddWithValue("@P", s.Price);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetActiveAsync(int id, bool active)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("UPDATE Services SET IsActive = @A WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@A", active);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<Service>> GetForProviderAsync(int providerId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.IsActive
            FROM Services s INNER JOIN ProviderServices ps ON ps.ServiceId = s.Id
            WHERE ps.ProviderId = @P AND s.IsActive = 1
            ORDER BY s.Name", conn);
        cmd.Parameters.AddWithValue("@P", providerId);
        var list = new List<Service>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task SetProviderServicesAsync(int providerId, IEnumerable<int> serviceIds)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var del = new SqlCommand("DELETE FROM ProviderServices WHERE ProviderId = @P", conn, tx))
            {
                del.Parameters.AddWithValue("@P", providerId);
                await del.ExecuteNonQueryAsync();
            }
            foreach (var sid in serviceIds.Distinct())
            {
                using var ins = new SqlCommand(
                    "INSERT INTO ProviderServices (ProviderId, ServiceId) VALUES (@P, @S)", conn, tx);
                ins.Parameters.AddWithValue("@P", providerId);
                ins.Parameters.AddWithValue("@S", sid);
                await ins.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<IEnumerable<int>> GetProviderServiceIdsAsync(int providerId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT ServiceId FROM ProviderServices WHERE ProviderId = @P", conn);
        cmd.Parameters.AddWithValue("@P", providerId);
        var list = new List<int>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetInt32(0));
        return list;
    }

    private static Service Map(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0), Name = r.GetString(1),
        Description = r.IsDBNull(2) ? null : r.GetString(2),
        DurationMinutes = r.GetInt32(3),
        Price = r.GetDecimal(4),
        IsActive = r.GetBoolean(5)
    };
}
