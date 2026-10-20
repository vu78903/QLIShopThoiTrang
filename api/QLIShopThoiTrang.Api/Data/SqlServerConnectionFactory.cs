using Npgsql;

namespace QLIShopThoiTrang.Api.Data;

// Tạo kết nối dùng chung tới PostgreSQL trên Supabase.
public sealed class SqlServerConnectionFactory(IConfiguration configuration)
{
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("YODYSTORE_CONNECTION_STRING")
        ?? configuration.GetConnectionString("YodyStoreDb")
        ?? throw new InvalidOperationException("Thiếu biến môi trường YODYSTORE_CONNECTION_STRING.");

    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
