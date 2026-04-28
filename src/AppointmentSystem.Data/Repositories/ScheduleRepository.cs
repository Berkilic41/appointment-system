using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace AppointmentSystem.Data.Repositories;

public class ScheduleRepository : IScheduleRepository
{
    private readonly DbConnectionFactory _factory;
    public ScheduleRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<WorkingHour>> GetWorkingHoursAsync(int providerId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT Id, ProviderId, DayOfWeek, StartMinutes, EndMinutes FROM WorkingHours WHERE ProviderId = @P ORDER BY DayOfWeek, StartMinutes", conn);
        cmd.Parameters.AddWithValue("@P", providerId);
        var list = new List<WorkingHour>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new WorkingHour
            {
                Id = r.GetInt32(0), ProviderId = r.GetInt32(1),
                DayOfWeek = r.GetByte(2),
                StartMinutes = r.GetInt16(3), EndMinutes = r.GetInt16(4)
            });
        return list;
    }

    public async Task ReplaceWorkingHoursAsync(int providerId, IEnumerable<WorkingHour> hours)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var del = new SqlCommand("DELETE FROM WorkingHours WHERE ProviderId = @P", conn, tx))
            {
                del.Parameters.AddWithValue("@P", providerId);
                await del.ExecuteNonQueryAsync();
            }
            foreach (var h in hours.Where(x => x.StartMinutes < x.EndMinutes))
            {
                using var ins = new SqlCommand(
                    "INSERT INTO WorkingHours (ProviderId, DayOfWeek, StartMinutes, EndMinutes) VALUES (@P, @D, @S, @E)", conn, tx);
                ins.Parameters.AddWithValue("@P", providerId);
                ins.Parameters.AddWithValue("@D", (byte)h.DayOfWeek);
                ins.Parameters.AddWithValue("@S", h.StartMinutes);
                ins.Parameters.AddWithValue("@E", h.EndMinutes);
                await ins.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<IEnumerable<TimeOff>> GetTimeOffAsync(int providerId, DateTime fromUtc, DateTime toUtc)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT Id, ProviderId, StartUtc, EndUtc, Reason
            FROM TimeOff
            WHERE ProviderId = @P AND StartUtc < @To AND EndUtc > @From
            ORDER BY StartUtc", conn);
        cmd.Parameters.AddWithValue("@P", providerId);
        cmd.Parameters.AddWithValue("@From", fromUtc);
        cmd.Parameters.AddWithValue("@To", toUtc);
        var list = new List<TimeOff>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new TimeOff
            {
                Id = r.GetInt32(0), ProviderId = r.GetInt32(1),
                StartUtc = r.GetDateTime(2), EndUtc = r.GetDateTime(3),
                Reason = r.IsDBNull(4) ? null : r.GetString(4)
            });
        return list;
    }

    public async Task<int> CreateTimeOffAsync(TimeOff entry)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            INSERT INTO TimeOff (ProviderId, StartUtc, EndUtc, Reason) OUTPUT INSERTED.Id
            VALUES (@P, @S, @E, @R)", conn);
        cmd.Parameters.AddWithValue("@P", entry.ProviderId);
        cmd.Parameters.AddWithValue("@S", entry.StartUtc);
        cmd.Parameters.AddWithValue("@E", entry.EndUtc);
        cmd.Parameters.AddWithValue("@R", (object?)entry.Reason ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task DeleteTimeOffAsync(int id, int providerId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("DELETE FROM TimeOff WHERE Id = @Id AND ProviderId = @P", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@P", providerId);
        await cmd.ExecuteNonQueryAsync();
    }
}
