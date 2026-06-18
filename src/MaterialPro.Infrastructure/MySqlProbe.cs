using MySqlConnector;

namespace MaterialPro.Infrastructure;

public sealed class MySqlProbe
{
    public async Task<bool> CanConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection.State == System.Data.ConnectionState.Open;
    }
}
