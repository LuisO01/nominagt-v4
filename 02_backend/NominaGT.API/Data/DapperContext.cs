using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace NominaGT.API.Data;

/// <summary>
/// Factory de conexiones Oracle. Cada llamada a CreateConnection() devuelve
/// una nueva instancia que el caller debe disponer (using var conn = ...).
/// </summary>
public class DapperContext
{
    private readonly string _connectionString;

    public DapperContext(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("NominaGT")
            ?? throw new InvalidOperationException(
                "ConnectionString 'NominaGT' no configurada en appsettings.json");
    }

    public IDbConnection CreateConnection()
    {
        var conn = new OracleConnection(_connectionString);
        // BindByName=true permite usar parametros nombrados :Param en cualquier orden
        conn.Open();
        return conn;
    }
}
