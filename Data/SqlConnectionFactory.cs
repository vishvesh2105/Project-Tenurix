using Microsoft.Data.SqlClient;

namespace Capstone.Api.Data;

public sealed class SqlConnectionFactory
{
    private readonly string _cs;

    public SqlConnectionFactory(IConfiguration config)
    {
        _cs = config.GetConnectionString("AzureSql")
              ?? throw new InvalidOperationException("Missing ConnectionStrings:AzureSql");
    }

    public SqlConnection Create() => new SqlConnection(_cs);
}
