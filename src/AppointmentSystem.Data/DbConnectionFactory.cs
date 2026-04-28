using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AppointmentSystem.Data;

public class DbConnectionFactory
{
    private readonly string _connectionString;
    public DbConnectionFactory(IConfiguration config) =>
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    public SqlConnection CreateConnection() => new(_connectionString);
}
