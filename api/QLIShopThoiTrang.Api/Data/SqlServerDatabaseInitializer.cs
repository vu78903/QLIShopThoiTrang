using System.Text.Json;
using Npgsql;

namespace QLIShopThoiTrang.Api.Data;

// Giữ tên lớp để tương thích DI; khởi động chỉ đọc, tuyệt đối không sửa schema/dữ liệu.
public sealed class SqlServerDatabaseInitializer(SqlServerConnectionFactory db)
{
    public async Task InitializeAsync()
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync();

        using var contract = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Data", "SchemaContract.json")));
        var columns = new Dictionary<string, string>();
        await using (var command = new NpgsqlCommand("""
            SELECT c.relname,a.attname,format_type(a.atttypid,a.atttypmod)
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            JOIN pg_attribute a ON a.attrelid=c.oid
            WHERE n.nspname='public' AND c.relkind IN ('r','v')
              AND a.attnum>0 AND NOT a.attisdropped
            """, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) columns[reader.GetString(0) + "." + reader.GetString(1)] = reader.GetString(2);

        var problems = new List<string>();
        foreach (var table in contract.RootElement.GetProperty("tables").EnumerateArray())
        {
            var name = table.GetProperty("name").GetString();
            foreach (var column in table.GetProperty("columns").EnumerateArray())
            {
                var key = name + "." + column.GetProperty("name").GetString();
                if (!columns.TryGetValue(key, out var actual)) problems.Add("thiếu " + key);
                else if (actual != column.GetProperty("type").GetString()) problems.Add("sai kiểu " + key);
            }
        }
        foreach (var key in new[] { "sanpham(masku)", "chitiethoadon(sohd,masku)", "chitietdoitra(sophieudt,masku)" })
        {
            var table = key[..key.IndexOf('(')];
            await using var command = new NpgsqlCommand("""
                SELECT replace(replace(pg_get_constraintdef(oid),'PRIMARY KEY ',''),' ','')
                FROM pg_constraint WHERE conrelid=to_regclass('public.' || @table) AND contype='p'
                """, connection, transaction);
            command.Parameters.AddWithValue("table", table);
            if (await command.ExecuteScalarAsync() as string != key[table.Length..]) problems.Add("sai khóa chính " + table);
        }
        await using (var command = new NpgsqlCommand("""
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema='public' AND table_name='hoadon'
              AND column_name IN ('datrutonkho','daghinhanthanhtoan')
              AND column_default IN ('false','false::boolean')
            """, connection, transaction))
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 2) problems.Add("thiếu mặc định FALSE cho cờ tồn/thanh toán");
        // Chỉ đọc: hai trigger là phần bắt buộc để phát hiện thay đổi bảo mật qua mọi ứng dụng/SQL.
        await using (var command = new NpgsqlCommand("""
            SELECT count(*) FROM pg_trigger t
            JOIN pg_class c ON c.oid=t.tgrelid
            JOIN pg_namespace n ON n.oid=c.relnamespace
            JOIN pg_proc p ON p.oid=t.tgfoid
            JOIN pg_namespace pn ON pn.oid=p.pronamespace
            WHERE n.nspname='public' AND pn.nspname='public'
              AND NOT t.tgisinternal AND t.tgtype=23 AND t.tgenabled IN ('O','A')
              AND ((c.relname='nhanvien' AND t.tgname='trg_phienbanbaomat_nhanvien' AND p.proname='capnhatphienbanbaomatnhanvien')
                OR (c.relname='phanquyen' AND t.tgname='trg_phienbanbaomat_phanquyen' AND p.proname='capnhatphienbanbaomatphanquyen'))
            """,connection,transaction))
            if(Convert.ToInt32(await command.ExecuteScalarAsync())!=2)problems.Add("thiếu trigger phiên bản bảo mật đang hoạt động trên nhanvien/phanquyen");
        if (problems.Count > 0)
            throw new InvalidOperationException("Schema chưa tương thích: " + string.Join("; ", problems) +
                ". Đối chiếu đúng phần cấu trúc được báo với quản trị database; API không tự sửa database.");
        await transaction.CommitAsync();
    }
}
