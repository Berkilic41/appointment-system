using AppointmentSystem.Data.Entities;
using AppointmentSystem.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace AppointmentSystem.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DbConnectionFactory _factory;
    public UserRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<User?> GetByEmailAsync(string email) => await OneAsync("WHERE Email = @V", email);
    public async Task<User?> GetByIdAsync(int id) => await OneAsync("WHERE Id = @V", id);

    public async Task<bool> ExistsByEmailAsync(string email) => await ExistsAsync("Email = @V", email);
    public async Task<bool> ExistsByUsernameAsync(string username) => await ExistsAsync("Username = @V", username);

    public async Task<int> CreateAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            INSERT INTO Users (Username, Email, PasswordHash, PasswordSalt, Role, FullName, Phone)
            OUTPUT INSERTED.Id
            VALUES (@U, @E, @H, @S, @R, @F, @P)", conn);
        cmd.Parameters.AddWithValue("@U", user.Username);
        cmd.Parameters.AddWithValue("@E", user.Email);
        cmd.Parameters.AddWithValue("@H", user.PasswordHash);
        cmd.Parameters.AddWithValue("@S", user.PasswordSalt);
        cmd.Parameters.AddWithValue("@R", user.Role);
        cmd.Parameters.AddWithValue("@F", (object?)user.FullName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@P", (object?)user.Phone ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT Id, Username, Email, PasswordHash, PasswordSalt, Role, FullName, Phone, IsActive, CreatedAt FROM Users ORDER BY CreatedAt DESC", conn);
        var list = new List<User>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task UpdateRoleAsync(int id, string role)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("UPDATE Users SET Role = @R WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@R", role);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetActiveAsync(int id, bool active)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand("UPDATE Users SET IsActive = @A WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@A", active);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<ProviderProfile>> GetAllProvidersAsync()
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT u.Id, u.Username, u.FullName, u.Email, p.Bio, p.Specialty,
                   ISNULL((SELECT AVG(CAST(Stars AS FLOAT)) FROM Ratings WHERE ProviderId = u.Id), 0) AS Avg,
                   ISNULL((SELECT COUNT(*) FROM Ratings WHERE ProviderId = u.Id), 0) AS Cnt
            FROM Users u
            LEFT JOIN ProviderProfiles p ON p.UserId = u.Id
            WHERE u.Role = 'Provider' AND u.IsActive = 1
            ORDER BY u.FullName", conn);
        var list = new List<ProviderProfile>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(MapProvider(r));
        return list;
    }

    public async Task<ProviderProfile?> GetProviderProfileAsync(int providerId)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            SELECT u.Id, u.Username, u.FullName, u.Email, p.Bio, p.Specialty,
                   ISNULL((SELECT AVG(CAST(Stars AS FLOAT)) FROM Ratings WHERE ProviderId = u.Id), 0),
                   ISNULL((SELECT COUNT(*) FROM Ratings WHERE ProviderId = u.Id), 0)
            FROM Users u LEFT JOIN ProviderProfiles p ON p.UserId = u.Id
            WHERE u.Id = @Id AND u.Role = 'Provider'", conn);
        cmd.Parameters.AddWithValue("@Id", providerId);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapProvider(r) : null;
    }

    public async Task UpsertProviderProfileAsync(int providerId, string? bio, string? specialty)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
            IF EXISTS (SELECT 1 FROM ProviderProfiles WHERE UserId = @Id)
                UPDATE ProviderProfiles SET Bio = @B, Specialty = @S WHERE UserId = @Id;
            ELSE
                INSERT INTO ProviderProfiles (UserId, Bio, Specialty) VALUES (@Id, @B, @S);", conn);
        cmd.Parameters.AddWithValue("@Id", providerId);
        cmd.Parameters.AddWithValue("@B", (object?)bio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@S", (object?)specialty ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<bool> ExistsAsync(string where, object value)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand($"SELECT COUNT(1) FROM Users WHERE {where}", conn);
        cmd.Parameters.AddWithValue("@V", value);
        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }

    private async Task<User?> OneAsync(string where, object value)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand($@"
            SELECT Id, Username, Email, PasswordHash, PasswordSalt, Role, FullName, Phone, IsActive, CreatedAt
            FROM Users {where}", conn);
        cmd.Parameters.AddWithValue("@V", value);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Map(r) : null;
    }

    private static User Map(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0), Username = r.GetString(1), Email = r.GetString(2),
        PasswordHash = r.GetString(3), PasswordSalt = r.GetString(4), Role = r.GetString(5),
        FullName = r.IsDBNull(6) ? null : r.GetString(6),
        Phone = r.IsDBNull(7) ? null : r.GetString(7),
        IsActive = r.GetBoolean(8), CreatedAt = r.GetDateTime(9)
    };

    private static ProviderProfile MapProvider(SqlDataReader r) => new()
    {
        UserId = r.GetInt32(0), Username = r.GetString(1),
        FullName = r.IsDBNull(2) ? null : r.GetString(2),
        Email = r.IsDBNull(3) ? null : r.GetString(3),
        Bio = r.IsDBNull(4) ? null : r.GetString(4),
        Specialty = r.IsDBNull(5) ? null : r.GetString(5),
        AverageRating = r.GetDouble(6),
        RatingCount = r.GetInt32(7)
    };
}
