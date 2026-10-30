using System.Data;
using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Models;
using FashionStore.Backend.Middleware;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Services;

public sealed class CartService(FashionStoreDbContext db, TimeProvider clock, DatabaseMode? mode = null)
{
    public Task<CartResponse> GetAsync(string customerId, CancellationToken ct) => ReadAsync(customerId, ct);

    // PUT quantity is absolute, so retries do not add the same quantity twice.
    // POST add is additive and intentionally not idempotent.
    public async Task<CartResponse> SetAsync(string customerId, string sku, int quantity, bool add, CancellationToken ct)
    {
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await LockCustomerAsync(customerId, ct);
            var cart = await db.Carts.AsTracking().SingleOrDefaultAsync(c => c.MaKH == customerId, ct);
            var newCart = cart == null;
            if (cart == null)
            {
                var now = clock.GetLocalNow().DateTime;
                cart = new GioHang { MaKH = customerId, NgayTao = now, NgayCapNhat = now };
                db.Carts.Add(cart);
            }
            BienTheSanPham? variant;
            bool active;
            if (db.Database.IsNpgsql())
            {
                var row = await db.Set<SupabaseProduct>().AsNoTracking().Include(p => p.Category).SingleOrDefaultAsync(p => p.masku == sku, ct);
                variant = row == null ? null : new BienTheSanPham { MaSKU = row.masku, MaSP = row.masp, SoLuongTonKho = row.soluongtonkho };
                active = row != null && row.trangthai && row.Category.trangthai;
            }
            else
            {
                variant = await db.Variants.SingleOrDefaultAsync(v => v.MaSKU == sku, ct);
                var product = variant == null ? null : await db.Products.Include(p => p.Loai).SingleOrDefaultAsync(p => p.MaSP == variant.MaSP, ct);
                active = variant != null && product != null && variant.TrangThai && product.TrangThai && product.Loai.TrangThai;
            }
            if (variant == null || !active)
                throw new CustomerRequestException(404, "Phân loại sản phẩm hiện không được kinh doanh.");
            // Match existing lines in SQL using the database collation; store the canonical SKU.
            var line = newCart ? null : await db.CartLines.AsTracking()
                .SingleOrDefaultAsync(l => l.MaGioHang == cart!.MaGioHang && l.MaSKU == variant.MaSKU, ct);
            var next = add ? (long)(line?.SoLuong ?? 0) + quantity : quantity;
            if (next < 1 || next > int.MaxValue) throw new CustomerRequestException(400, "Số lượng không hợp lệ.");
            if (next > variant.SoLuongTonKho) throw new CustomerRequestException(409, "Số lượng vượt quá tồn kho hiện tại.");
            if (line == null) cart!.Lines.Add(new ChiTietGioHang { MaSKU = variant.MaSKU, SoLuong = (int)next, NgayCapNhat = clock.GetLocalNow().DateTime });
            else { line.SoLuong = (int)next; line.NgayCapNhat = clock.GetLocalNow().DateTime; }
            cart!.NgayCapNhat = clock.GetLocalNow().DateTime;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return await ReadAsync(customerId, ct);
    }
    public async Task<CartResponse> DeleteAsync(string customerId, string? sku, CancellationToken ct)
    {
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            await LockCustomerAsync(customerId, ct);
            var cart = await db.Carts.AsTracking().SingleOrDefaultAsync(c => c.MaKH == customerId, ct);
            if (cart != null)
            {
                var lines = await db.CartLines.AsTracking()
                    .Where(l => l.MaGioHang == cart.MaGioHang && (sku == null || l.MaSKU == sku)).ToArrayAsync(ct);
                db.CartLines.RemoveRange(lines);
                cart.NgayCapNhat = clock.GetLocalNow().DateTime;
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        return await ReadAsync(customerId, ct);
    }
    private async Task LockCustomerAsync(string id, CancellationToken ct)
    {
        // Customer PK lock serializes cart creation/mutations for one owner, including old accounts without a cart.
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.khachhang WHERE makh={id} FOR UPDATE", ct);
        var rows = db.Database.IsNpgsql()
            ? await db.Customers.AsNoTracking().Where(c => c.MaKH == id).ToListAsync(ct)
            : await db.Customers.FromSqlInterpolated($"SELECT * FROM dbo.KhachHang WITH (UPDLOCK,HOLDLOCK) WHERE MaKH={id}").AsNoTracking().ToListAsync(ct);
        if (rows.Count != 1 || !rows[0].TrangThai) throw new CustomerRequestException(401, "Vui lòng đăng nhập lại.");
    }
    internal sealed record CartProjection(string MaSKU, int SoLuong, string MaSP, string TenSanPham,
        string KichCo, string MauSac, decimal DonGia, int SoLuongTonKho, bool Active, string? ImageUrl);
    internal IQueryable<CartProjection> ReadQuery(string id) => db.Database.IsNpgsql()
        ? db.CartLines.Where(l => l.Cart.MaKH == id)
            .Join(db.Set<SupabaseProduct>(), l => l.MaSKU, p => p.masku, (l, p) => new { Line = l, Product = p })
            .OrderBy(x => x.Line.MaSKU)
            .Select(x => new CartProjection(x.Line.MaSKU, x.Line.SoLuong, x.Product.masp, x.Product.tensanpham,
                x.Product.kichco ?? "", x.Product.mausac ?? "", x.Product.dongia, x.Product.soluongtonkho,
                x.Product.trangthai && x.Product.Category.trangthai, x.Product.hinhanh))
        : db.CartLines.Where(l => l.Cart.MaKH == id)
            .Join(db.Products, l => l.Variant.MaSP, p => p.MaSP, (l, p) => new { Line = l, Product = p })
            .OrderBy(x => x.Line.MaSKU)
            .Select(x => new CartProjection(x.Line.MaSKU, x.Line.SoLuong, x.Product.MaSP, x.Product.TenSanPham,
                x.Line.Variant.KichCo, x.Line.Variant.MauSac, x.Line.Variant.DonGia, x.Line.Variant.SoLuongTonKho,
                x.Line.Variant.TrangThai && x.Product.TrangThai && x.Product.Loai.TrangThai,
                x.Product.Images.Where(i=>i.MaSKU==null||i.MaSKU==x.Line.MaSKU).OrderByDescending(i=>i.AnhChinh).ThenBy(i=>i.ThuTu).Select(i=>i.DuongDan).FirstOrDefault()));
    private async Task<CartResponse> ReadAsync(string id, CancellationToken ct)
    {
        var rows = await ReadQuery(id).ToListAsync(ct);
        var items = rows.Select(l => new CartLineResponse(l.MaSKU, l.MaSP, l.TenSanPham, l.KichCo, l.MauSac,
            l.SoLuong, l.DonGia, l.SoLuong * l.DonGia, l.SoLuongTonKho, l.Active && l.SoLuong <= l.SoLuongTonKho,
            !l.Active ? "Sản phẩm đã ngừng kinh doanh." : l.SoLuong > l.SoLuongTonKho ? "Số lượng trong giỏ vượt tồn kho hiện tại." : null,l.ImageUrl)).ToArray();
        return new CartResponse(items, items.Sum(l => (long)l.Quantity), items.Sum(l => l.LineTotal), items.Length > 0 && items.All(l => l.Available))
        { CheckoutEnabled = !db.Database.IsNpgsql() || (mode?.OrdersEnabled == true && !string.IsNullOrWhiteSpace(mode.WebOrderEmployeeId)), CodOnly = db.Database.IsNpgsql() };
    }
}
