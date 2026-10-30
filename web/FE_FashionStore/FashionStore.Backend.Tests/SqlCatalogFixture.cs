using Microsoft.Data.SqlClient;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FASHIONSTORE_TEST_CONNECTION")))
            Skip = "BLOCKED: set FASHIONSTORE_TEST_CONNECTION to a restored FashionStoreDB_Dev SQL Server database.";
    }
}

[CollectionDefinition("SqlCatalog", DisableParallelization = true)]
public sealed class SqlCatalogCollection : ICollectionFixture<SqlCatalogFixture> { }

// Integration fixture writes only tagged catalog test rows in an already restored
// FashionStoreDB_Dev copy, then removes those rows. It never creates a database.
public sealed class SqlCatalogFixture : IAsyncLifetime
{
    public string Tag { get; } = "TAPI" + Guid.NewGuid().ToString("N")[..8];
    public string Connection { get; private set; } = "";
    public ApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string Product(int number) => Tag + "-" + number.ToString("00");
    public string CategoryA => Tag + "A";
    public string CategoryB => Tag + "B";

    public async Task InitializeAsync()
    {
        Connection = Environment.GetEnvironmentVariable("FASHIONSTORE_TEST_CONNECTION") ?? "";
        if (string.IsNullOrWhiteSpace(Connection)) return;
        if (new SqlConnectionStringBuilder(Connection).InitialCatalog != "FashionStoreDB_Dev")
            throw new InvalidOperationException("SQL integration tests require FashionStoreDB_Dev, never the main database.");
        await using var sql = new SqlConnection(Connection);
        await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = """
            IF DB_NAME() <> N'FashionStoreDB_Dev' THROW 51000, 'DEV copy required.', 1;
            BEGIN TRANSACTION;
            INSERT dbo.LoaiSanPham(MaLoai,TenLoai,TrangThai)
            VALUES(@tag+'A',@tag+N' Áo',1),(@tag+'B',@tag+N' Quần',1),(@tag+'C',@tag+N' Ẩn',0);
            INSERT dbo.SanPham(MaSP,MaLoai,TenSanPham,MoTa,DonGiaNiemYet,TrangThai,NgayTao,DoiTuong)
            VALUES
              (@tag+'-01',@tag+'A',@tag+N' Áo sơ mi',N'Mô tả tích hợp SQL',999999,1,'2026-01-01','NAM'),
              (@tag+'-02',@tag+'B',@tag+N' Quần nữ',NULL,999999,1,'2026-01-02','NU'),
              (@tag+'-03',@tag+'A',@tag+N' Áo unisex',NULL,999999,1,'2026-01-03','UNISEX'),
              (@tag+'-04',@tag+'B',@tag+N' Quần trẻ em',NULL,999999,1,'2026-01-04','TRE_EM'),
              (@tag+'-05',@tag+'A',@tag+N' SP ngừng bán',NULL,999999,0,'2026-01-05','NAM'),
              (@tag+'-06',@tag+'A',@tag+N' SKU ngừng bán',NULL,999999,1,'2026-01-06','NAM'),
              (@tag+'-07',@tag+'C',@tag+N' Loại ngừng bán',NULL,999999,1,'2026-01-07','NAM'),
              (@tag+'-08',@tag+'A',@tag+N' Không SKU',NULL,999999,1,'2026-01-08','NAM'),
              (@tag+'-09',@tag+'A',@tag+N' Chưa phân nhóm',NULL,999999,1,'2026-01-09',NULL);
            INSERT dbo.BienTheSanPham(MaSKU,MaSP,KichCo,MauSac,DonGia,SoLuongTonKho,TrangThai)
            VALUES
              (@tag+'-01-M',@tag+'-01',N'M',N'Xanh',100000,2,1),
              (@tag+'-01-L',@tag+'-01',N'L',N'Đỏ',200000,0,1),
              (@tag+'-01-X',@tag+'-01',N'X',N'Ẩn',1,99,0),
              (@tag+'-02-S',@tag+'-02',N'S',N'Đen',300000,0,1),
              (@tag+'-03-F',@tag+'-03',N'F',N'Trắng',150000,3,1),
              (@tag+'-04-XS',@tag+'-04',N'XS',N'Vàng',50000,1,1),
              (@tag+'-05-M',@tag+'-05',N'M',N'Xanh',100000,2,1),
              (@tag+'-06-M',@tag+'-06',N'M',N'Xanh',100000,2,0),
              (@tag+'-07-M',@tag+'-07',N'M',N'Xanh',100000,2,1),
              (@tag+'-09-M',@tag+'-09',N'M',N'Xanh',250000,1,1);
            INSERT dbo.HinhAnhSanPham(MaSP,MaSKU,DuongDan,AnhChinh,ThuTu)
            VALUES(@tag+'-01',NULL,N'/images/integration-main.jpg',1,0),
                  (@tag+'-01',@tag+'-01-M',N'/images/integration-blue.jpg',1,1),
                  (@tag+'-01',@tag+'-01-X',N'/images/integration-hidden.jpg',1,2);
            COMMIT TRANSACTION;
            """;
        command.Parameters.AddWithValue("@tag", Tag);
        await command.ExecuteNonQueryAsync();
        Factory = new ApiFactory(Connection);
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory != null) await Factory.DisposeAsync();
        if (string.IsNullOrWhiteSpace(Connection)) return;
        await using var sql = new SqlConnection(Connection);
        await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = """
            IF DB_NAME() <> N'FashionStoreDB_Dev' THROW 51000, 'DEV copy required.', 1;
            BEGIN TRANSACTION;
            DELETE dbo.HinhAnhSanPham WHERE MaSP LIKE @tag+'-%';
            DELETE dbo.BienTheSanPham WHERE MaSP LIKE @tag+'-%';
            DELETE dbo.SanPham WHERE MaSP LIKE @tag+'-%';
            DELETE dbo.LoaiSanPham WHERE MaLoai IN (@tag+'A',@tag+'B',@tag+'C');
            COMMIT TRANSACTION;
            """;
        command.Parameters.AddWithValue("@tag", Tag);
        await command.ExecuteNonQueryAsync();
    }
}
