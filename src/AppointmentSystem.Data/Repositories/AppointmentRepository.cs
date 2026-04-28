using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AppointmentSystem.Data.Repositories;

public class AppointmentRepository : IAppointmentRepository
{
    private readonly DbConnectionFactory _factory;
    public AppointmentRepository(DbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Books an appointment using a serializable transaction with explicit
    /// (UPDLOCK, HOLDLOCK) range lock to prevent double-booking races.
    /// Throws BookingConflictException if an overlap is detected.
    /// </summary>
    public async Task<int> CreateWithConflictCheckAsync(Appointment a)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            using (var check = new SqlCommand(@"
                SELECT TOP 1 Id FROM Appointments WITH (UPDLOCK, HOLDLOCK)
                WHERE ProviderId = @P
                  AND Status IN ('Pending','Confirmed')
                  AND StartUtc < @End
                  AND EndUtc   > @Start", conn, tx))
            {
                check.Parameters.AddWithValue("@P", a.ProviderId);
                check.Parameters.AddWithValue("@Start", a.StartUtc);
                check.Parameters.AddWithValue("@End",   a.EndUtc);
                var existing = await check.ExecuteScalarAsync();
                if (existing is not null && existing is not DBNull)
                {
                    await tx.RollbackAsync();
                    throw new BookingConflictException("This time slot is no longer available.");
                }
            }

            int id;
            using (var ins = new SqlCommand(@"
                INSERT INTO Appointments (ProviderId, CustomerId, ServiceId, StartUtc, EndUtc, Status, Notes)
                OUTPUT INSERTED.Id
                VALUES (@P, @C, @S, @Start, @End, @Status, @N)", conn, tx))
            {
                ins.Parameters.AddWithValue("@P", a.ProviderId);
                ins.Parameters.AddWithValue("@C", a.CustomerId);
                ins.Parameters.AddWithValue("@S", a.ServiceId);
                ins.Parameters.AddWithValue("@Start", a.StartUtc);
                ins.Parameters.AddWithValue("@End", a.EndUtc);
                ins.Parameters.AddWithValue("@Status", a.Status);
                ins.Parameters.AddWithValue("@N", (object?)a.Notes ?? DBNull.Value);
                id = (int)(await ins.ExecuteScalarAsync())!;
            }

            await tx.CommitAsync();
            return id;
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            // unique-index violation on UX_Appt_Provider_Start_Active = same exact start
            try { await tx.RollbackAsync(); } catch { }
            throw new BookingConflictException("This time slot is no longer available.");
        }
        catch
        {
            try { await tx.RollbackAsync(); } catch { }
            throw;
        }
    }

    public async Task<Appointment?> GetByIdAsync(int id)
    {
        var rows = await QueryAsync("WHERE a.Id = @V", ("@V", id));
        return rows.FirstOrDefault();
    }

    public Task<IEnumerable<Appointment>> GetForProviderAsync(int providerId, DateTime fromUtc, DateTime toUtc)
        => QueryAsync("WHERE a.ProviderId = @P AND a.StartUtc >= @F AND a.StartUtc < @T",
                      ("@P", providerId), ("@F", fromUtc), ("@T", toUtc));

    public Task<IEnumerable<Appointment>> GetForCustomerAsync(int customerId, bool includePast = true)
        => QueryAsync(includePast
            ? "WHERE a.CustomerId = @C"
            : "WHERE a.CustomerId = @C AND a.EndUtc >= GETUTCDATE()", ("@C", customerId));

    public Task<IEnumerable<Appointment>> GetInRangeAsync(DateTime fromUtc, DateTime toUtc)
        => QueryAsync("WHERE a.StartUtc >= @F AND a.StartUtc < @T", ("@F", fromUtc), ("@T", toUtc));

    public async Task UpdateStatusAsync(int id, string status)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "UPDATE Appointments SET Status = @S, UpdatedAt = GETUTCDATE() WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@S", status);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<ProviderUtilizationRow>> GetUtilizationAsync(DateTime fromUtc, DateTime toUtc)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("sp_GetProviderUtilization", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@FromUtc", fromUtc);
        cmd.Parameters.AddWithValue("@ToUtc", toUtc);
        var list = new List<ProviderUtilizationRow>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ProviderUtilizationRow
            {
                Id = r.GetInt32(0), Username = r.GetString(1),
                FullName = r.IsDBNull(2) ? null : r.GetString(2),
                MinutesBooked = r.GetInt32(3),
                AppointmentCount = r.GetInt32(4),
                AverageRating = r.GetDouble(5),
                RatingCount = r.GetInt32(6)
            });
        return list;
    }

    public async Task<IEnumerable<DailyCount>> GetDailyCountsAsync(DateTime fromUtc, DateTime toUtc)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("sp_GetDailyAppointmentCounts", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@FromUtc", fromUtc);
        cmd.Parameters.AddWithValue("@ToUtc", toUtc);
        var list = new List<DailyCount>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new DailyCount
            {
                Day = r.GetDateTime(0),
                Total = r.GetInt32(1), Pending = r.GetInt32(2),
                Confirmed = r.GetInt32(3), Completed = r.GetInt32(4),
                Cancelled = r.GetInt32(5)
            });
        return list;
    }

    private async Task<IEnumerable<Appointment>> QueryAsync(string where, params (string, object)[] parameters)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand($@"
            SELECT a.Id, a.ProviderId, a.CustomerId, a.ServiceId, a.StartUtc, a.EndUtc, a.Status, a.Notes,
                   a.CreatedAt, a.UpdatedAt,
                   p.FullName AS ProviderName, c.FullName AS CustomerName, c.Email AS CustomerEmail,
                   s.Name AS ServiceName,
                   (SELECT Stars FROM Ratings WHERE AppointmentId = a.Id) AS RatingStars
            FROM Appointments a
            INNER JOIN Users p ON p.Id = a.ProviderId
            INNER JOIN Users c ON c.Id = a.CustomerId
            INNER JOIN Services s ON s.Id = a.ServiceId
            {where}
            ORDER BY a.StartUtc", conn);
        foreach (var (n, v) in parameters) cmd.Parameters.AddWithValue(n, v);
        var list = new List<Appointment>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(Map(r));
        return list;
    }

    private static Appointment Map(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0), ProviderId = r.GetInt32(1), CustomerId = r.GetInt32(2),
        ServiceId = r.GetInt32(3), StartUtc = r.GetDateTime(4), EndUtc = r.GetDateTime(5),
        Status = r.GetString(6),
        Notes = r.IsDBNull(7) ? null : r.GetString(7),
        CreatedAt = r.GetDateTime(8), UpdatedAt = r.GetDateTime(9),
        ProviderName = r.IsDBNull(10) ? null : r.GetString(10),
        CustomerName = r.IsDBNull(11) ? null : r.GetString(11),
        CustomerEmail = r.IsDBNull(12) ? null : r.GetString(12),
        ServiceName = r.GetString(13),
        RatingStars = r.IsDBNull(14) ? null : r.GetByte(14)
    };
}
