using Npgsql;
using QLIShopThoiTrang.Api.Models;
namespace QLIShopThoiTrang.Api.Data;
public sealed partial class StoreRepository
{
    public async Task<List<BestSellingProductDto>> GetBestSellingProductsAsync(DateOnly? from, DateOnly? to)
    {
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        await using var command=NewCommand(connection,null,$"""
            SELECT s.MaSP,MAX(s.TenSanPham),SUM(c.SoLuong)::bigint,COUNT(DISTINCT h.SoHD)
            FROM ChiTietHoaDon c JOIN HoaDon h ON h.SoHD=c.SoHD
            JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE h.TrangThaiThanhToan='DA_THANH_TOAN' AND NOT {CancelledShipmentSql}
              AND (@from::date IS NULL OR h.NgayLap>=@from::date)
              AND (@to::date IS NULL OR h.NgayLap<(@to::date + INTERVAL '1 day'))
            GROUP BY s.MaSP HAVING SUM(c.SoLuong)>0
            ORDER BY SUM(c.SoLuong) DESC,s.MaSP
            """,("@from",from.HasValue?(object)from.Value:DBNull.Value),("@to",to.HasValue?(object)to.Value:DBNull.Value));
        var result=new List<BestSellingProductDto>();
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetInt64(2),reader.GetInt64(3)));
        return result;
    }
}
