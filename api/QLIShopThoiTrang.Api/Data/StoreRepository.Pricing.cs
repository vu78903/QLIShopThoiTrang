using Npgsql;
using QLIShopThoiTrang.Api.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QLIShopThoiTrang.Api.Data;

public sealed partial class StoreRepository
{
    private static decimal RoundMoney(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
    private static string Fingerprint<T>(T input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));

    private static async Task<decimal> EffectivePriceAsync(NpgsqlConnection connection, NpgsqlTransaction? tx, string sku)
    {
        await using var command = NewCommand(connection, tx, """
            SELECT LEAST(sp.DonGia,COALESCE(km.GiaKhuyenMai,sp.DonGia))
            FROM SanPham sp LEFT JOIN SanPhamKhuyenMai_HienTai km ON km.MaSKU=sp.MaSKU
            WHERE sp.MaSKU=@sku AND sp.TrangThai=TRUE
            """, ("@sku", sku));
        var result = await command.ExecuteScalarAsync();
        if (result is null or DBNull) throw new InvalidOperationException($"SKU {sku} không còn hoạt động.");
        return Convert.ToDecimal(result);
    }

    private static async Task ValidateInvoicePricingAsync(NpgsqlConnection connection, NpgsqlTransaction? tx, InvoiceCreateDto input)
    {
        if (input.Items.Count == 0 || input.Items.Any(i => i.Qty <= 0 || i.Price < 0 || i.Total != i.Price*i.Qty))
            throw new InvalidOperationException("Số lượng hoặc thành tiền từng dòng không hợp lệ.");
        foreach (var item in input.Items)
            if (item.Price != await EffectivePriceAsync(connection, tx, item.Sku))
                throw new InvalidOperationException("Giá hoặc khuyến mãi sản phẩm đã thay đổi. Hãy tải lại giỏ hàng.");
        var subtotal = input.Items.Sum(i => i.Price*i.Qty);
        decimal memberDiscount = 0, voucherDiscount = 0;
        var phone = Regex.Replace(input.Customer?.Phone ?? "", @"\D", "");
        if (phone.Length > 0)
        {
            await using var customer = NewCommand(connection, tx,
                "SELECT TongChiTieu FROM KhachHang WHERE SoDienThoai=@phone AND TrangThai=TRUE" + (tx is null ? "" : " FOR UPDATE"), ("@phone",phone));
            var spending = await customer.ExecuteScalarAsync();
            if (spending is not null and not DBNull)
            {
                var total = Convert.ToDecimal(spending);
                memberDiscount = RoundMoney(subtotal * (total > 15000000m ? 10 : total > 5000000m ? 5 : 0)/100m);
            }
        }
        if (!string.IsNullOrWhiteSpace(input.PromoCode))
        {
            await using var voucher = NewCommand(connection, tx, """
                SELECT GiaTriGiam,LoaiKM FROM KhuyenMai k
                WHERE UPPER(k.MaKM)=UPPER(@code) AND k.TrangThai=TRUE
                  AND (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh') BETWEEN k.NgayBatDau AND k.NgayKetThuc
                  AND k.GiaTriDonHangToiThieu<=@subtotal
                  AND NOT EXISTS(SELECT 1 FROM KhuyenMaiSanPham p WHERE p.MaKM=k.MaKM)
                """, ("@code",input.PromoCode.Trim()),("@subtotal",subtotal));
            await using var reader = await voucher.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("Voucher hết hạn, chưa đủ điều kiện hoặc là chương trình giảm riêng SKU.");
            var value = reader.GetDecimal(0);
            var kind = ToAscii(reader.GetString(1)).Replace("_", " ");
            voucherDiscount = kind is "SO TIEN" or "VND" ? RoundMoney(value) : RoundMoney(subtotal*value/100m);
        }
        var discount = Math.Min(subtotal, memberDiscount+voucherDiscount);
        var vat = RoundMoney((subtotal-discount)*0.08m);
        var shipping = (input.IsCod || !string.IsNullOrWhiteSpace(input.DeliveryMode)) ? input.ShippingFee : 0;
        if (shipping < 0 || input.Subtotal != subtotal || input.Discount != discount || input.VatAmount != vat || input.Total != subtotal-discount+vat+shipping)
            throw new InvalidOperationException("Tạm tính, chiết khấu, VAT hoặc tổng tiền không khớp. Hãy tải lại giỏ hàng.");
    }

    // Phân bổ bằng tổng lũy kế: tổng các dòng luôn bằng đúng tiền hàng thực trả, không gồm phí giao.
    private static Dictionary<string,decimal> AllocatePaidLines(IEnumerable<InvoiceItemDto> items, decimal paidTotal)
    {
        var lines=items.OrderBy(i=>i.Sku,StringComparer.Ordinal).ToList();
        var total=lines.Sum(i=>i.Price*i.Qty);
        decimal cumulative=0,allocated=0;
        var result=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in lines)
        {
            cumulative+=line.Price*line.Qty;
            var next=total==0 ? 0 : RoundMoney(paidTotal*cumulative/total);
            result[line.Sku]=next-allocated;allocated=next;
        }
        return result;
    }

    private static async Task<Dictionary<string,decimal>> PaidLineTotalsAsync(NpgsqlConnection connection, NpgsqlTransaction? tx, string id)
    {
        var items=new List<InvoiceItemDto>();var snapshots=new Dictionary<string,decimal>();decimal total=0;
        await using var command=NewCommand(connection,tx,"""
            SELECT c.MaSKU,c.SoLuong,c.DonGia,c.GiaTriThucTra,GREATEST(0,h.KhachPhaiTra-COALESCE(h.PhiShip,0))
            FROM ChiTietHoaDon c JOIN HoaDon h ON h.SoHD=c.SoHD WHERE c.SoHD=@id ORDER BY c.MaSKU
            """,("@id",id));
        await using(var reader=await command.ExecuteReaderAsync())while(await reader.ReadAsync())
        {
            var sku=reader.GetString(0);var qty=reader.GetInt32(1);var price=reader.GetDecimal(2);
            items.Add(new(sku,"",qty,price,qty*price));total=reader.GetDecimal(4);
            if(!reader.IsDBNull(3))snapshots[sku]=reader.GetDecimal(3);
        }
        var amounts=AllocatePaidLines(items,total);
        foreach(var entry in snapshots)amounts[entry.Key]=entry.Value;
        return amounts;
    }

    private static decimal RefundForQuantity(decimal paidTotal, int purchased, int returned, decimal refunded, int qty)
        => Math.Min(Math.Max(0,paidTotal-refunded),Math.Max(0,RoundMoney(paidTotal*(returned+qty)/purchased)-RoundMoney(paidTotal*returned/purchased)));
}
