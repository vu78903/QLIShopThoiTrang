using Npgsql;
using QLIShopThoiTrang.Api.Models;
namespace QLIShopThoiTrang.Api.Data;
public sealed partial class StoreRepository
{
    public async Task<int> GetProductStockAsync(string sku)
    {
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        return (int)await ScalarLongAsync(connection,null,"SELECT COALESCE(SoLuongTonKho,0) FROM SanPham WHERE MaSKU=@sku",("@sku",sku));
    }

    public async Task<AppStateDto> GetWarehouseAppStateAsync(string employeeId)
    {
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        var partners=new List<PartnerDto>();
        await using(var command=NewCommand(connection,null,"""
            SELECT 'Nhà cung cấp',MaNSX,TenNSX,SoDienThoai,COALESCE(Email,''),COALESCE(DiaChi,''),COALESCE(MaSoThue,'')
            FROM NhaSanXuat WHERE TrangThai=TRUE
            UNION ALL SELECT 'Nhân viên',MaNV,HoTen,'','','','Nhân viên kho'
            FROM NhanVien WHERE MaNV=@employee AND TrangThai=TRUE ORDER BY 1,2
            """,("@employee",employeeId)))
        await using(var reader=await command.ExecuteReaderAsync())
            while(await reader.ReadAsync())partners.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),null,null));
        return new(await QueryProductsAsync(connection),await QueryProductTypesAsync(connection),partners,[],await QueryImportRequestsAsync(connection),[],[],[],[]);
    }
}
