using Npgsql;
using QLIShopThoiTrang.Api.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QLIShopThoiTrang.Api.Data;

// Chứa toàn bộ câu lệnh đọc và ghi dữ liệu cho các form WinForms.
public sealed class StoreRepository(SqlServerConnectionFactory db)
{
    public async Task<string> GetDatabaseNameAsync()
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        return $"{connection.DataSource}/{connection.Database}";
    }

    public async Task<AppStateDto> GetAppStateAsync()
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        return new AppStateDto(
            await QueryProductsAsync(connection),
            await QueryProductTypesAsync(connection),
            await QueryPartnersAsync(connection),
            await QueryPromotionsAsync(connection),
            await QueryImportRequestsAsync(connection),
            await QuerySalesHistoryAsync(connection),
            await QueryDeliveryOrdersAsync(connection),
            await QueryCodOrdersAsync(connection));
    }

    public async Task<List<ProductTypeDto>> GetProductTypesAsync()
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        return await QueryProductTypesAsync(connection);
    }

    // Lấy danh sách hóa đơn gần nhất để thu ngân tra cứu và in lại.
    public async Task<List<InvoiceSummaryDto>> GetInvoicesAsync(string? keyword)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        var search = $"%{(keyword ?? string.Empty).Trim()}%";
        var items = new List<InvoiceSummaryDto>();
        await using var command = NewCommand(connection, null, """
            SELECT h.SoHD,h.NgayLap,COALESCE(k.HoTen,'Khách lẻ'),
                   COALESCE(h.SoDienThoai_KH,''),nv.HoTen,h.PhuongThucThanhToan,
                   COALESCE(SUM(c.SoLuong),0),h.KhachPhaiTra,h.TrangThaiThanhToan
            FROM HoaDon h
            JOIN NhanVien nv ON nv.MaNV=h.MaNV
            LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            LEFT JOIN ChiTietHoaDon c ON c.SoHD=h.SoHD
            WHERE (@keyword='%%' OR h.SoHD LIKE @keyword OR k.HoTen LIKE @keyword
                   OR h.SoDienThoai_KH LIKE @keyword)
            GROUP BY h.SoHD,h.NgayLap,k.HoTen,h.SoDienThoai_KH,nv.HoTen,
                     h.PhuongThucThanhToan,h.KhachPhaiTra,h.TrangThaiThanhToan
            ORDER BY h.NgayLap DESC LIMIT 200
            """, ("@keyword", search));
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new(reader.GetString(0), reader.GetDateTime(1).ToString("dd/MM/yyyy HH:mm"),
                reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetInt32(6), reader.GetDecimal(7), reader.GetString(8)));
        return items;
    }

    // Đọc đầy đủ một hóa đơn từ SQL Server để nội dung in lại đúng với lần bán.
    public async Task<InvoiceDetailDto?> GetInvoiceAsync(string invoiceId)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = NewCommand(connection, null, """
            SELECT h.SoHD,h.NgayLap,nv.HoTen,COALESCE(k.HoTen,'Khách lẻ'),
                   COALESCE(h.SoDienThoai_KH,''),COALESCE(k.HangThe,'Khách vãng lai'),
                   COALESCE(k.DiemTichLuy,0),h.TongCong,h.ChietKhau,h.ThueVAT,
                   COALESCE(h.PhiShip,0),h.KhachPhaiTra,h.PhuongThucThanhToan,h.TienKhachDua,
                   COALESCE(h.MaKM,''),CASE WHEN UPPER(h.LoaiHoaDon) IN ('COD','WEB_COD') THEN TRUE ELSE FALSE END,
                   CASE WHEN UPPER(h.LoaiHoaDon) IN ('COD','WEB_COD') THEN 'COD'
                        WHEN UPPER(h.LoaiHoaDon) IN ('GIAO_HANG','WEB_DA_THANH_TOAN','WEB_THANH_TOAN') THEN 'DELIVERY'
                        ELSE '' END
            FROM HoaDon h
            JOIN NhanVien nv ON nv.MaNV=h.MaNV
            LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            WHERE h.SoHD=@id
            """, ("@id", invoiceId));

        string id;
        DateTime createdAt;
        string cashier;
        string customerName;
        string customerPhone;
        string customerRank;
        int customerPoints;
        decimal subtotal;
        decimal discount;
        decimal vatAmount;
        decimal shippingFee;
        decimal total;
        string paymentMethod;
        decimal paid;
        string promoCode;
        bool isCod;
        string deliveryMode;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) return null;
            id = reader.GetString(0);
            createdAt = reader.GetDateTime(1);
            cashier = reader.GetString(2);
            customerName = reader.GetString(3);
            customerPhone = reader.GetString(4);
            customerRank = reader.GetString(5);
            customerPoints = reader.GetInt32(6);
            subtotal = reader.GetDecimal(7);
            discount = reader.GetDecimal(8);
            vatAmount = reader.GetDecimal(9);
            shippingFee = reader.GetDecimal(10);
            total = reader.GetDecimal(11);
            paymentMethod = reader.GetString(12);
            paid = reader.GetDecimal(13);
            promoCode = reader.GetString(14);
            isCod = reader.GetBoolean(15);
            deliveryMode = reader.GetString(16);
        }

        var details = new List<InvoiceDetailItemDto>();
        await using var detailCommand = NewCommand(connection, null, """
            SELECT c.MaSKU,s.TenSanPham,s.MauSac,s.KichCo,s.DonViTinh,
                   c.SoLuong,c.DonGia,c.ThanhTien
            FROM ChiTietHoaDon c JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE c.SoHD=@id ORDER BY c.MaSKU
            """, ("@id", invoiceId));
        await using var detailReader = await detailCommand.ExecuteReaderAsync();
        while (await detailReader.ReadAsync())
            details.Add(new(detailReader.GetString(0),detailReader.GetString(1),detailReader.GetString(2),
                detailReader.GetString(3),detailReader.GetString(4),detailReader.GetInt32(5),
                detailReader.GetDecimal(6),detailReader.GetDecimal(7)));

        return new InvoiceDetailDto(id,createdAt.ToString("dd/MM/yyyy HH:mm"),cashier,customerName,
            customerPhone,customerRank,customerPoints,details,details.Sum(x => x.Qty),subtotal,
            discount,8,vatAmount,shippingFee,total,paymentMethod,paid,(int)Math.Floor(total/10000m),promoCode,isCod,deliveryMode);
    }

    // Xác thực tài khoản nhân viên và trả về mã vai trò mà form chính đang dùng.
    public async Task<LoginResultDto?> LoginAsync(LoginRequestDto input)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = NewCommand(connection, null, """
            SELECT nv.MaNV,nv.HoTen,nv.MaQuyen
            FROM NhanVien nv JOIN PhanQuyen pq ON pq.MaQuyen=nv.MaQuyen
            WHERE nv.TenDangNhap=@username AND nv.MatKhau=@password
              AND nv.TrangThai=TRUE AND pq.TrangThai=TRUE LIMIT 1
            """, ("@username", input.Username.Trim()), ("@password", input.Password));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var role = reader.GetString(2) switch
        {
            "CHT" => "CuaHangTruong",
            "TC" => "TruongCa",
            "KHO" => "NhanVienKho",
            _ => "ThuNgan"
        };
        return new LoginResultDto(reader.GetString(0),reader.GetString(1),role);
    }

    public async Task SaveProductAsync(ProductUpsertDto input)
    {
        var sku = (input.Sku ?? string.Empty).Trim().ToUpperInvariant();
        var productId = (input.ProductId ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(productId, @"^SP-\d{3,}$"))
            throw new InvalidOperationException("Mã sản phẩm phải có dạng SP-001.");
        if (!Regex.IsMatch(sku, @"^[A-Z0-9][A-Z0-9-]{2,19}$"))
            throw new InvalidOperationException("Mã SKU chỉ được gồm chữ, số, dấu gạch ngang và tối đa 20 ký tự.");

        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        var type = input.Type.Trim();
        var color = string.IsNullOrWhiteSpace(input.Color) ? "Mặc định" : input.Color.Trim();
        var size = string.IsNullOrWhiteSpace(input.Size) ? "Free size" : input.Size.Trim();
        var maLoai = await ScalarStringAsync(connection, tx,
            "SELECT MaLoai FROM LoaiSanPham WHERE TrangThai=TRUE AND (TenLoai=@type OR MaLoai=@type) LIMIT 1",
            ("@type", type));
        if (maLoai is null) throw new InvalidOperationException("Loại sản phẩm không tồn tại.");

        var duplicateVariant = await ScalarLongAsync(connection, tx, """
            SELECT COUNT(*) FROM SanPham
            WHERE MaSP=@productId AND lower(trim(MauSac))=lower(trim(@color))
              AND lower(trim(KichCo))=lower(trim(@size)) AND MaSKU<>@sku
            """, ("@productId", productId), ("@color", color), ("@size", size), ("@sku", sku));
        if (duplicateVariant > 0)
            throw new InvalidOperationException("Sản phẩm đã có biến thể màu và size này.");

        await ExecuteAsync(connection, tx, """
            INSERT INTO SanPham(MaSP,MaSKU,MaLoai,TenSanPham,SoLuong,KichCo,MauSac,TrangThai,DonGia,SoLuongTonKho,HinhAnh)
            VALUES(@productId,@sku,@type,@name,@stock,@size,@color,TRUE,@price,@stock,@image)
            ON CONFLICT (MaSKU) DO UPDATE SET
                MaSP=EXCLUDED.MaSP,MaLoai=EXCLUDED.MaLoai,TenSanPham=EXCLUDED.TenSanPham,DonGia=EXCLUDED.DonGia,
                SoLuong=EXCLUDED.SoLuong,SoLuongTonKho=EXCLUDED.SoLuongTonKho,
                KichCo=EXCLUDED.KichCo,MauSac=EXCLUDED.MauSac,HinhAnh=EXCLUDED.HinhAnh,TrangThai=TRUE;
            """,
            ("@sku", sku), ("@productId", productId), ("@type", maLoai), ("@name", input.Name.Trim()),
            ("@price", input.Price), ("@stock", input.Stock), ("@color", color),
            ("@size", size),
            ("@image", DbValue(input.Image)));
        await tx.CommitAsync();
    }

    public async Task<ProductUpsertDto> ResolveProductIdentityAsync(ProductUpsertDto input)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        var productId = (input.ProductId ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(productId))
        {
            await using var existing = NewCommand(connection, null, """
                SELECT sp.MaSP
                FROM SanPham sp JOIN LoaiSanPham l ON l.MaLoai=sp.MaLoai
                WHERE lower(trim(sp.TenSanPham))=lower(trim(@name))
                  AND (l.TenLoai=@type OR l.MaLoai=@type)
                ORDER BY sp.MaSP LIMIT 1
                """, ("@name", input.Name.Trim()), ("@type", input.Type.Trim()));
            productId = Convert.ToString(await existing.ExecuteScalarAsync()) ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(productId))
        {
            await using var next = NewCommand(connection, null, """
                SELECT COALESCE(MAX(NULLIF(SUBSTRING(MaSP FROM 4),'')::bigint),0)+1
                FROM SanPham WHERE MaSP LIKE 'SP-%'
                """);
            productId = $"SP-{Convert.ToInt64(await next.ExecuteScalarAsync()):000}";
        }

        var sku = (input.Sku ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(sku))
        {
            var sequence = Regex.Match(productId, @"\d+$").Value;
            var namePart = ProductSkuCode(input.Name);
            var colorPart = ColorSkuCode(input.Color ?? "Mặc định");
            var sizePart = SizeSkuCode(input.Size ?? "Free size");
            sku = $"{namePart}-{sequence}-{colorPart}-{sizePart}";
            if (sku.Length > 20) sku = sku[..20].TrimEnd('-');

            var baseSku = sku;
            var suffix = 2;
            while (await ProductSkuExistsAsync(connection, sku))
            {
                var tail = $"-{suffix++}";
                sku = baseSku[..Math.Min(baseSku.Length, 20 - tail.Length)].TrimEnd('-') + tail;
            }
        }
        return input with { ProductId = productId, Sku = sku };
    }

    private static async Task<bool> ProductSkuExistsAsync(NpgsqlConnection connection, string sku)
    {
        await using var command = NewCommand(connection, null,
            "SELECT COUNT(*) FROM SanPham WHERE MaSKU=@sku", ("@sku", sku));
        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    private static string ToSkuPart(string value, int maxLength)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(ch =>
            CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant()
            .Replace('Đ', 'D');
        var part = Regex.Replace(ascii, @"[^A-Z0-9]+", string.Empty);
        if (string.IsNullOrWhiteSpace(part)) part = "SP";
        return part[..Math.Min(part.Length, maxLength)];
    }

    private static string ProductSkuCode(string name)
    {
        var text = ToAscii(name);
        if (Regex.IsMatch(text, @"QUAN.*JEANS?")) return "QJ";
        if (Regex.IsMatch(text, @"QUAN.*KAKI")) return "QK";
        (string Pattern, string Code)[] rules =
        [
            ("HOODIE", "HD"), ("AO THUN", "AT"), ("SO MI", "ASM"), ("POLO", "AP"),
            ("AO KHOAC", "AK"), ("VEST", "AV"), ("JOGGER", "QJG"), ("QUAN NGAN", "QN"), ("SHORT", "QN"),
            ("VAY", "VAY"), ("DAM", "DAM"), ("MAT KINH", "MK"), ("DAY NIT", "DN"), ("DAY NICH", "DN"),
            ("NON", "NON"), ("VO", "VO")
        ];
        foreach (var (pattern, code) in rules)
            if (text.Contains(pattern, StringComparison.Ordinal)) return code;
        var initials = string.Concat(Regex.Split(text, @"[^A-Z0-9]+")
            .Where(word => word.Length > 0).Select(word => word[0]));
        return string.IsNullOrWhiteSpace(initials) ? "SP" : initials[..Math.Min(initials.Length, 4)];
    }

    private static string ColorSkuCode(string color)
    {
        var text = ToAscii(color);
        var colors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DEN"]="DEN", ["TRANG"]="TRANG", ["XAM"]="XAM", ["DO"]="DO", ["VANG"]="VANG",
            ["CAM"]="CAM", ["HONG"]="HONG", ["TIM"]="TIM", ["NAU"]="NAU", ["BE"]="BE",
            ["XANH DUONG"]="XANHD", ["XANH LA"]="XANHL", ["XANH DAM"]="XANHDAM", ["XANH NAVY"]="NAVY"
        };
        return colors.GetValueOrDefault(text) ?? ToSkuPart(text, 7);
    }

    private static string SizeSkuCode(string size) =>
        ToAscii(size) == "FREE SIZE" ? "FS" : ToSkuPart(size, 4);

    private static string ToAscii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).ToUpperInvariant().Replace('Đ', 'D').Trim();
    }

    // Nhập cả file trong một transaction: chỉ cần một dòng lỗi thì không sản phẩm nào được ghi.
    public async Task ImportProductsAsync(IReadOnlyList<ProductUpsertDto> inputs)
    {
        var fileSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var productId = (input.ProductId ?? string.Empty).Trim().ToUpperInvariant();
            if (!Regex.IsMatch(productId, @"^SP-\d{3,}$"))
                throw new InvalidOperationException($"Dòng {index + 2}: Mã SP phải có dạng SP-023.");
            if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Type))
                throw new InvalidOperationException($"Dòng {index + 2}: tên và loại sản phẩm là bắt buộc.");
            if (input.Price <= 0 || input.Stock < 0)
                throw new InvalidOperationException($"Dòng {index + 2}: giá hoặc tồn kho không hợp lệ.");

            var color = string.IsNullOrWhiteSpace(input.Color) ? "Mặc định" : input.Color.Trim();
            var size = string.IsNullOrWhiteSpace(input.Size) ? "Free size" : input.Size.Trim();
            if (!fileVariants.Add($"{productId}|{color}|{size}"))
                throw new InvalidOperationException($"Dòng {index + 2}: biến thể {productId} - {color} - {size} bị lặp trong file.");

            var sku = (input.Sku ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(sku))
            {
                var sequence = Regex.Match(productId, @"\d+$").Value;
                sku = $"{ProductSkuCode(input.Name)}-{sequence}-{ColorSkuCode(color)}-{SizeSkuCode(size)}";
                if (sku.Length > 20) sku = sku[..20].TrimEnd('-');
                var baseSku = sku;
                var suffix = 2;
                while (fileSkus.Contains(sku) || await ProductSkuExistsAsync(connection, sku))
                {
                    var tail = $"-{suffix++}";
                    sku = baseSku[..Math.Min(baseSku.Length, 20 - tail.Length)].TrimEnd('-') + tail;
                }
            }
            if (!Regex.IsMatch(sku, @"^[A-Z0-9][A-Z0-9-]{2,19}$"))
                throw new InvalidOperationException($"Dòng {index + 2}: Mã SKU không hợp lệ.");
            if (!fileSkus.Add(sku))
                throw new InvalidOperationException($"Dòng {index + 2}: Mã SKU {sku} bị lặp trong file.");
            if (await ScalarLongAsync(connection, tx,
                    "SELECT COUNT(*) FROM SanPham WHERE MaSKU=@sku",
                    ("@sku", sku)) > 0)
                throw new InvalidOperationException($"Dòng {index + 2}: Mã SKU {sku} đã tồn tại trong cơ sở dữ liệu.");
            if (await ScalarLongAsync(connection, tx, """
                    SELECT COUNT(*) FROM SanPham
                    WHERE MaSP=@productId AND lower(trim(MauSac))=lower(trim(@color))
                      AND lower(trim(KichCo))=lower(trim(@size))
                    """, ("@productId", productId), ("@color", color), ("@size", size)) > 0)
                throw new InvalidOperationException($"Dòng {index + 2}: biến thể {productId} - {color} - {size} đã tồn tại.");

            var type = input.Type.Trim();
            var maLoai = await ScalarStringAsync(connection, tx,
                "SELECT MaLoai FROM LoaiSanPham WHERE TrangThai=TRUE AND (TenLoai=@type OR MaLoai=@type) LIMIT 1",
                ("@type", type));
            if (maLoai is null)
            {
                var nextType = await ScalarLongAsync(connection, tx, """
                    SELECT COALESCE(MAX(NULLIF(SUBSTRING(MaLoai FROM 5),'')::bigint),0)+1
                    FROM LoaiSanPham WHERE MaLoai LIKE 'LSP-%'
                    """);
                maLoai = $"LSP-{nextType:000}";
                await ExecuteAsync(connection, tx,
                    "INSERT INTO LoaiSanPham(MaLoai,TenLoai,MoTa,TrangThai) VALUES(@id,@name,'Loại tạo từ file import',TRUE)",
                    ("@id", maLoai), ("@name", type));
            }

            await ExecuteAsync(connection, tx, """
                INSERT INTO SanPham(MaSP,MaSKU,MaLoai,TenSanPham,SoLuong,KichCo,MauSac,TrangThai,DonGia,SoLuongTonKho,HinhAnh)
                VALUES(@productId,@sku,@type,@name,@stock,@size,@color,TRUE,@price,@stock,NULL)
                """,
                ("@productId", productId), ("@sku", sku), ("@type", maLoai), ("@name", input.Name.Trim()),
                ("@stock", input.Stock), ("@color", color), ("@size", size),
                ("@price", input.Price));
        }
        await tx.CommitAsync();
    }

    public async Task<string?> GetProductImageAsync(string sku)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = NewCommand(connection, null,
            "SELECT HinhAnh FROM SanPham WHERE MaSKU=@sku", ("@sku", sku));
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    // Chỉ xóa file Storage khi không còn biến thể màu/size nào dùng chung URL ảnh đó.
    public async Task<bool> IsProductImageInUseAsync(string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return false;
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = NewCommand(connection, null,
            "SELECT EXISTS(SELECT 1 FROM SanPham WHERE HinhAnh=@image)", ("@image", image));
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    // Xóa thật sản phẩm chưa phát sinh chứng từ; sản phẩm đã có lịch sử phải được giữ lại.
    public async Task<bool> DeleteProductAsync(string sku)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        var references = await ScalarLongAsync(connection, tx, """
            SELECT
                (SELECT COUNT(*) FROM ChiTietHoaDon WHERE MaSKU=@sku) +
                (SELECT COUNT(*) FROM ChiTietNhapKho WHERE MaSKU=@sku) +
                (SELECT COUNT(*) FROM ChiTietDoiTra WHERE MaSKU=@sku) +
                (SELECT COUNT(*) FROM ChiTietBaoCaoDoanhThu WHERE MaSKU=@sku) +
                (SELECT COUNT(*) FROM BienBanXuLyHangLoi WHERE MaSKU=@sku)
            """, ("@sku", sku));
        if (references > 0)
            throw new InvalidOperationException("Sản phẩm đã có chứng từ phát sinh nên không thể xóa khỏi lịch sử.");
        var affected = await ExecuteAsync(connection, tx,
            "DELETE FROM SanPham WHERE MaSKU=@sku", ("@sku", sku));
        await tx.CommitAsync();
        return affected > 0;
    }

    public async Task SaveProductTypeAsync(ProductTypeUpsertDto input) =>
        await ExecuteStandaloneAsync("""
            INSERT INTO LoaiSanPham(MaLoai,TenLoai,MoTa,TrangThai)
            VALUES(@id,@name,@description,@active)
            ON CONFLICT (MaLoai) DO UPDATE SET TenLoai=EXCLUDED.TenLoai,
                MoTa=EXCLUDED.MoTa,TrangThai=EXCLUDED.TrangThai;
            """,
            ("@id", input.Id.Trim()), ("@name", input.Name.Trim()),
            ("@description", DbValue(input.Description)), ("@active", input.Active));

    public async Task<bool> DeactivateProductTypeAsync(string maLoai) =>
        await ExecuteStandaloneAsync("UPDATE LoaiSanPham SET TrangThai=FALSE WHERE MaLoai=@id", ("@id", maLoai)) > 0;

    public async Task SavePartnerAsync(PartnerUpsertDto input)
    {
        if (input.Category == "Khách hàng")
        {
            await ExecuteStandaloneAsync("""
                INSERT INTO KhachHang(MaKH,SoDienThoai,HoTen,Email,DiaChi)
                VALUES(@id,@phone,@name,@email,@address)
                ON CONFLICT (MaKH) DO UPDATE SET HoTen=EXCLUDED.HoTen,
                    SoDienThoai=EXCLUDED.SoDienThoai,Email=EXCLUDED.Email,
                    DiaChi=EXCLUDED.DiaChi,TrangThai=TRUE;
                """, CommonPartnerParams(input));
            return;
        }
        if (input.Category == "Nhà cung cấp")
        {
            await ExecuteStandaloneAsync("""
                INSERT INTO NhaSanXuat(MaNSX,TenNSX,DiaChi,MaSoThue,SoDienThoai,Email)
                VALUES(@id,@name,@address,@role,@phone,@email)
                ON CONFLICT (MaNSX) DO UPDATE SET TenNSX=EXCLUDED.TenNSX,
                    DiaChi=EXCLUDED.DiaChi,MaSoThue=EXCLUDED.MaSoThue,
                    SoDienThoai=EXCLUDED.SoDienThoai,Email=EXCLUDED.Email,TrangThai=TRUE;
                """, CommonPartnerParams(input));
            return;
        }
        if (input.Category == "Nhân viên")
        {
            var parameters = CommonPartnerParams(input).Append(("@permission", (object?)MapPermission(input.Role))).ToArray();
            await ExecuteStandaloneAsync("""
                INSERT INTO NhanVien(MaNV,TenDangNhap,HoTen,ChucVu,SoDienThoai,Email,DiaChi,MaQuyen,MatKhau)
                VALUES(@id,LOWER(REPLACE(@id,'-','')),@name,@role,@phone,@email,@address,@permission,'123')
                ON CONFLICT (MaNV) DO UPDATE SET HoTen=EXCLUDED.HoTen,
                    ChucVu=EXCLUDED.ChucVu,SoDienThoai=EXCLUDED.SoDienThoai,
                    Email=EXCLUDED.Email,DiaChi=EXCLUDED.DiaChi,
                    MaQuyen=EXCLUDED.MaQuyen,TrangThai=TRUE;
                """, parameters);
            return;
        }
        throw new InvalidOperationException("Loại đối tác không hợp lệ.");
    }

    public async Task<bool> DeactivatePartnerAsync(string category, string id)
    {
        var sql = category switch
        {
            "Khách hàng" => "UPDATE KhachHang SET TrangThai=FALSE WHERE MaKH=@id",
            "Nhà cung cấp" => "UPDATE NhaSanXuat SET TrangThai=FALSE WHERE MaNSX=@id",
            "Nhân viên" => "UPDATE NhanVien SET TrangThai=FALSE WHERE MaNV=@id",
            _ => throw new InvalidOperationException("Loại đối tác không hợp lệ.")
        };
        return await ExecuteStandaloneAsync(sql, ("@id", id)) > 0;
    }

    public async Task SavePromotionAsync(PromotionUpsertDto input)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        await ExecuteStandaloneAsync("""
            INSERT INTO KhuyenMai(MaKM,TenChuongTrinh,LoaiKM,GiaTriGiam,GiaTriDonHangToiThieu,NgayBatDau,NgayKetThuc)
            VALUES(@code,@code,'Phần trăm',@percent,@minOrder,@startDate,@endDate)
            ON CONFLICT (MaKM) DO UPDATE SET TenChuongTrinh=EXCLUDED.TenChuongTrinh,
                LoaiKM=EXCLUDED.LoaiKM,GiaTriGiam=EXCLUDED.GiaTriGiam,
                GiaTriDonHangToiThieu=EXCLUDED.GiaTriDonHangToiThieu,
                NgayBatDau=EXCLUDED.NgayBatDau,NgayKetThuc=EXCLUDED.NgayKetThuc,TrangThai=TRUE;
            """,
            ("@code", code), ("@percent", input.Percent), ("@minOrder", input.MinOrder),
            ("@startDate", ParseDate(input.StartDate)), ("@endDate", ParseDate(input.EndDate)));
    }

    public async Task<bool> DeactivatePromotionAsync(string code) =>
        await ExecuteStandaloneAsync("UPDATE KhuyenMai SET TrangThai=FALSE WHERE MaKM=@code", ("@code", code)) > 0;

    public async Task<string> CreateImportRequestAsync(ImportRequestCreateDto input)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        var sku = input.Sku.Trim().ToUpperInvariant();
        var price = await ScalarDecimalAsync(connection, tx,
            "SELECT DonGia FROM SanPham WHERE MaSKU=@sku AND TrangThai=TRUE", ("@sku", sku));
        if (price is null) throw new InvalidOperationException("Mã sản phẩm không tồn tại.");
        var supplier = input.SupplierId.Trim();
        if (await ScalarLongAsync(connection, tx,
            "SELECT COUNT(*) FROM NhaSanXuat WHERE MaNSX=@id AND TrangThai=TRUE", ("@id", supplier)) == 0)
            throw new InvalidOperationException("Nhà cung cấp không tồn tại.");

        var employee = string.IsNullOrWhiteSpace(input.EmployeeId) ? "NV-003" : input.EmployeeId.Trim();
        var id = "PN-" + DateTime.Now.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var total = price.Value * input.Qty;
        await ExecuteAsync(connection, tx, """
            INSERT INTO PhieuNhapKho(SoPhieuNK,NgayLap,SoPO,NguoiGiao,TongSoLuong,TongTien,MaNSX,MaNV,TrangThai)
            VALUES(@id,CURRENT_TIMESTAMP,NULL,NULL,@qty,@total,@supplier,@employee,'CHO_DUYET');
            INSERT INTO ChiTietNhapKho(SoPhieuNK,MaSKU,SoLuong,DonGia,ThanhTien,GhiChu)
            VALUES(@id,@sku,@qty,@price,@total,@note);
            """,
            ("@id", id), ("@qty", input.Qty), ("@total", total), ("@supplier", supplier),
            ("@employee", employee), ("@sku", sku), ("@price", price.Value), ("@note", DbValue(input.Note)));
        await tx.CommitAsync();
        return id;
    }

    public async Task ApproveImportRequestAsync(string soPhieu)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        var status = await ScalarStringAsync(connection, tx,
            "SELECT TrangThai FROM PhieuNhapKho WHERE SoPhieuNK=@id", ("@id", soPhieu));
        if (status != "CHO_DUYET")
            throw new InvalidOperationException("Phiếu không tồn tại hoặc không ở trạng thái chờ duyệt.");
        await ExecuteAsync(connection, tx, """
            UPDATE SanPham sp SET SoLuongTonKho=sp.SoLuongTonKho+ct.SoLuong,
                SoLuong=sp.SoLuong+ct.SoLuong
            FROM ChiTietNhapKho ct
            WHERE ct.MaSKU=sp.MaSKU AND ct.SoPhieuNK=@id;
            UPDATE PhieuNhapKho SET TrangThai='DA_DUYET' WHERE SoPhieuNK=@id;
            """, ("@id", soPhieu));
        await tx.CommitAsync();
    }

    public async Task<bool> RejectImportRequestAsync(string soPhieu) =>
        await ExecuteStandaloneAsync("""
            UPDATE PhieuNhapKho SET TrangThai='TU_CHOI'
            WHERE SoPhieuNK=@id AND TrangThai='CHO_DUYET'
            """, ("@id", soPhieu)) > 0;

    // Lưu hóa đơn và chi tiết trong một transaction để không thể có hóa đơn thiếu sản phẩm.
    public async Task CreateInvoiceAsync(InvoiceCreateDto input)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        if (await ScalarLongAsync(connection, tx, "SELECT COUNT(*) FROM HoaDon WHERE SoHD=@id", ("@id", input.Id)) > 0)
            throw new InvalidOperationException("Mã hóa đơn đã tồn tại.");

        foreach (var item in input.Items)
        {
            var stock = await ScalarLongAsync(connection, tx,
                "SELECT COALESCE(MAX(SoLuongTonKho),-1) FROM SanPham WHERE MaSKU=@sku AND TrangThai=TRUE",
                ("@sku", item.Sku));
            if (stock < item.Qty)
                throw new InvalidOperationException($"Sản phẩm {item.Sku} không đủ tồn kho.");
        }

        var deliveryMode = (input.DeliveryMode ?? string.Empty).Trim().ToUpperInvariant();
        var isCod = input.IsCod || deliveryMode == "COD";
        var isDelivery = isCod || deliveryMode == "DELIVERY";
        var customerPhone = string.IsNullOrWhiteSpace(input.Customer?.Phone) ? null : Regex.Replace(input.Customer.Phone, @"\D", "");
        var receiverPhone = string.IsNullOrWhiteSpace(input.ReceiverPhone) ? null : Regex.Replace(input.ReceiverPhone, @"\D", "");
        var phone = isDelivery ? receiverPhone ?? customerPhone : customerPhone;
        var customerId = customerPhone is null ? null : await ScalarStringAsync(connection, tx,
            "SELECT MaKH FROM KhachHang WHERE SoDienThoai=@phone AND TrangThai=TRUE LIMIT 1", ("@phone", customerPhone));
        if (string.IsNullOrWhiteSpace(input.EmployeeId))
            throw new InvalidOperationException("Không xác định được nhân viên lập hóa đơn.");
        var employee = input.EmployeeId.Trim();
        if (await ScalarLongAsync(connection, tx,
                "SELECT COUNT(*) FROM NhanVien WHERE MaNV=@employee AND TrangThai=TRUE",
                ("@employee", employee)) == 0)
            throw new InvalidOperationException("Nhân viên lập hóa đơn không tồn tại hoặc đã ngừng hoạt động.");
        var promo = string.IsNullOrWhiteSpace(input.PromoCode) ? null : await ScalarStringAsync(connection, tx,
            "SELECT MaKM FROM KhuyenMai WHERE MaKM=@code AND TrangThai=TRUE", ("@code", input.PromoCode.Trim()));
        var invoiceType = isCod ? "COD" : isDelivery ? "GIAO_HANG" : "TAI_QUAY";
        var deliveryStatus = isDelivery ? "Vừa xác nhận đơn" : null;
        var paymentStatus = isCod ? "CHUA_THANH_TOAN" : "DA_THANH_TOAN";
        var source = string.Equals(input.Source, "WEB", StringComparison.OrdinalIgnoreCase) ? "WEB" : "POS";

        await ExecuteAsync(connection, tx, """
            INSERT INTO HoaDon(SoHD,NgayLap,LoaiHoaDon,PhuongThucThanhToan,TongCong,ChietKhau,ThueVAT,KhachPhaiTra,
                TienKhachDua,TienThua,SoDienThoai_KH,MaKH,TenNguoiNhan,DiaChiGiao,DonViVanChuyen,TrangThaiGiaoHang,
                TrangThaiThanhToan,PhiShip,TienCOD,ThoiGianDuKien,MaKM,MaNV,NguonDon,GhiChuGiaoHang)
            VALUES(@id,CURRENT_TIMESTAMP,@type,@payment,@subtotal,@discount,@vat,@total,@paid,
                CASE WHEN @paid>@total THEN @paid-@total ELSE 0 END,@phone,@customer,@receiver,@address,'Nội bộ',
                @delivery,@paymentStatus,@shippingFee,@cod,NULL,@promo,@employee,@source,@deliveryNote);
            """,
            ("@id", input.Id), ("@type", invoiceType),
            ("@payment", input.PaymentMethod), ("@subtotal", input.Subtotal), ("@discount", input.Discount),
            ("@vat", input.VatAmount), ("@total", input.Total), ("@paid", isCod ? 0 : input.Paid), ("@phone", phone), ("@customer", customerId),
            ("@receiver", isDelivery ? DbValue(input.ReceiverName) : DBNull.Value),
            ("@address", isDelivery ? DbValue(input.ReceiverAddress) : DBNull.Value), ("@delivery", deliveryStatus),
            ("@paymentStatus", paymentStatus), ("@shippingFee", isDelivery ? input.ShippingFee : 0), ("@cod", isCod ? input.Total : 0),
            ("@promo", promo), ("@employee", employee), ("@source", source),
            ("@deliveryNote", isDelivery ? DbValue(input.DeliveryNote) : DBNull.Value));

        foreach (var item in input.Items)
        {
            await ExecuteAsync(connection, tx, """
                INSERT INTO ChiTietHoaDon(SoHD,MaSKU,SoLuong,DonGia,ThanhTien)
                VALUES(@id,@sku,@qty,@price,@total);
                """,
                ("@id", input.Id), ("@sku", item.Sku), ("@qty", item.Qty),
                ("@price", item.Price), ("@total", item.Price * item.Qty));
        }

        if (!isCod)
            await CompleteInvoiceAsync(connection, tx, input.Id, customerId, input.Total);
        await tx.CommitAsync();
    }

    // Đơn COD chỉ trừ kho và tính doanh thu khi giao thành công; đơn đã thanh toán chỉ đổi trạng thái giao.
    public async Task UpdateDeliveryStatusAsync(string soHoaDon, CodStatusUpdateDto input)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        await using var command = NewCommand(connection, tx, """
            SELECT TrangThaiGiaoHang,MaKH,KhachPhaiTra,UPPER(LoaiHoaDon)
            FROM HoaDon
            WHERE SoHD=@id AND UPPER(LoaiHoaDon) IN ('COD','WEB_COD','GIAO_HANG','WEB_DA_THANH_TOAN','WEB_THANH_TOAN')
            """, ("@id", soHoaDon));
        string? oldStatus;
        string? customerId;
        decimal total;
        string invoiceType;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new InvalidOperationException("Không tìm thấy đơn giao hàng.");
            oldStatus = reader.IsDBNull(0) ? null : reader.GetString(0);
            customerId = reader.IsDBNull(1) ? null : reader.GetString(1);
            total = reader.GetDecimal(2);
            invoiceType = reader.GetString(3);
        }

        var statusFlow = new[] { "Vừa xác nhận đơn", "Shipper đã lấy hàng", "Hàng đang được vận chuyển", "Hàng giao thành công" };
        var oldIndex = Array.IndexOf(statusFlow, oldStatus ?? statusFlow[0]);
        var newIndex = Array.IndexOf(statusFlow, input.Status);
        if (newIndex < 0) throw new InvalidOperationException("Trạng thái giao hàng không hợp lệ.");
        if (oldStatus == "Hàng giao thành công" && input.Status != oldStatus)
            throw new InvalidOperationException("Đơn đã giao thành công không thể lùi trạng thái.");
        if (oldStatus != input.Status && newIndex != oldIndex + 1)
            throw new InvalidOperationException("Trạng thái đơn hàng phải được cập nhật theo đúng thứ tự.");

        var isCod = invoiceType is "COD" or "WEB_COD";
        if (isCod && input.Status == "Hàng giao thành công" && oldStatus != input.Status)
            await CompleteInvoiceAsync(connection, tx, soHoaDon, customerId, total);

        await ExecuteAsync(connection, tx, """
            UPDATE HoaDon SET TrangThaiGiaoHang=@status,
                TrangThaiThanhToan=CASE WHEN @isCod=TRUE AND @status='Hàng giao thành công' THEN 'DA_THANH_TOAN' ELSE TrangThaiThanhToan END,
                NgayGiaoThanhCong=CASE WHEN @status='Hàng giao thành công' AND NgayGiaoThanhCong IS NULL
                                      THEN CURRENT_TIMESTAMP ELSE NgayGiaoThanhCong END
            WHERE SoHD=@id
            """, ("@status", input.Status), ("@isCod", isCod), ("@id", soHoaDon));
        await tx.CommitAsync();
    }

    public async Task<bool> DeletePendingCodAsync(string soHoaDon)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();
        var exists = await ScalarLongAsync(connection, tx, """
            SELECT COUNT(*) FROM HoaDon
            WHERE SoHD=@id AND UPPER(LoaiHoaDon) IN ('COD','WEB_COD') AND COALESCE(TrangThaiGiaoHang,'')<>'Hàng giao thành công'
            """, ("@id", soHoaDon));
        if (exists == 0) return false;
        await ExecuteAsync(connection, tx,
            "DELETE FROM ChiTietHoaDon WHERE SoHD=@id; DELETE FROM HoaDon WHERE SoHD=@id;", ("@id", soHoaDon));
        await tx.CommitAsync();
        return true;
    }

    private static async Task CompleteInvoiceAsync(NpgsqlConnection connection, NpgsqlTransaction tx,
        string invoiceId, string? customerId, decimal total)
    {
        var unavailable = await ScalarLongAsync(connection, tx, """
            SELECT COUNT(*) FROM ChiTietHoaDon ct
            JOIN SanPham sp ON sp.MaSKU=ct.MaSKU
            WHERE ct.SoHD=@id AND sp.SoLuongTonKho<ct.SoLuong
            """, ("@id", invoiceId));
        if (unavailable > 0) throw new InvalidOperationException("Tồn kho không đủ để hoàn tất hóa đơn.");

        await ExecuteAsync(connection, tx, """
            UPDATE SanPham sp SET SoLuongTonKho=sp.SoLuongTonKho-ct.SoLuong,
                SoLuong=sp.SoLuong-ct.SoLuong
            FROM ChiTietHoaDon ct
            WHERE ct.MaSKU=sp.MaSKU AND ct.SoHD=@id;
            """, ("@id", invoiceId));

        if (customerId is not null)
        {
            await ExecuteAsync(connection, tx, """
                UPDATE KhachHang
                SET TongChiTieu=TongChiTieu+@total,
                    DiemTichLuy=DiemTichLuy+FLOOR(@total/10000)::int,
                    HangThe=CASE
                        WHEN TongChiTieu+@total>15000000 THEN 'Kim Cương'
                        WHEN TongChiTieu+@total>5000000 THEN 'Vàng'
                        ELSE 'Bạc' END
                WHERE MaKH=@customer;
                """, ("@total", total), ("@customer", customerId));
        }
    }

    // Tìm hóa đơn bằng mã hoặc số điện thoại để nhân viên bắt đầu đổi trả.
    public async Task<List<ReturnInvoiceSummaryDto>> SearchReturnInvoicesAsync(string keyword)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        var search = $"%{keyword.Trim()}%";
        var items = new List<ReturnInvoiceSummaryDto>();
        await using var command = NewCommand(connection, null, """
            SELECT h.SoHD,h.NgayLap,COALESCE(k.HoTen,'Không cung cấp'),
                   COALESCE(h.SoDienThoai_KH,''),h.LoaiHoaDon,h.TrangThaiGiaoHang,
                   h.NgayGiaoThanhCong,h.TrangThaiThanhToan
            FROM HoaDon h
            LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            WHERE (h.SoHD LIKE @search OR h.SoDienThoai_KH LIKE @search)
            ORDER BY h.NgayLap DESC LIMIT 30
            """, ("@search", search));
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var createdAt = reader.GetDateTime(1);
            var sourceCode = reader.GetString(4);
            var deliveryStatus = reader.IsDBNull(5) ? null : reader.GetString(5);
            DateTime? deliveredAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
            var paymentStatus = reader.GetString(7);
            var eligibility = GetReturnEligibility(sourceCode, createdAt, deliveryStatus, deliveredAt, paymentStatus);
            items.Add(new(reader.GetString(0), createdAt.ToString("dd/MM/yyyy HH:mm"), reader.GetString(2),
                reader.GetString(3), MapInvoiceSource(sourceCode), eligibility.StartDateText,
                eligibility.DeadlineText, eligibility.Allowed, eligibility.Message));
        }
        return items;
    }

    // Đọc các dòng hàng còn có thể đổi của một hóa đơn.
    public async Task<ReturnInvoiceDetailDto?> GetReturnInvoiceAsync(string invoiceId)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = NewCommand(connection, null, """
            SELECT h.SoHD,h.NgayLap,COALESCE(k.HoTen,'Không cung cấp'),COALESCE(h.SoDienThoai_KH,''),
                   h.LoaiHoaDon,h.TrangThaiGiaoHang,h.NgayGiaoThanhCong,h.TrangThaiThanhToan
            FROM HoaDon h LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            WHERE h.SoHD=@id
            """, ("@id", invoiceId));

        string id;
        DateTime createdAt;
        string customerName;
        string phone;
        string sourceCode;
        string? deliveryStatus;
        DateTime? deliveredAt;
        string paymentStatus;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) return null;
            id = reader.GetString(0);
            createdAt = reader.GetDateTime(1);
            customerName = reader.GetString(2);
            phone = reader.GetString(3);
            sourceCode = reader.GetString(4);
            deliveryStatus = reader.IsDBNull(5) ? null : reader.GetString(5);
            deliveredAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
            paymentStatus = reader.GetString(7);
        }

        var lines = new List<ReturnInvoiceItemDto>();
        await using var detailCommand = NewCommand(connection, null, """
            SELECT c.MaSKU,s.TenSanPham,s.MauSac,s.KichCo,s.DonViTinh,c.SoLuong,
                   COALESCE((SELECT SUM(dt.SoLuong)
                           FROM ChiTietDoiTra dt
                           JOIN PhieuDoiTra pdt ON pdt.SoPhieuDT=dt.SoPhieuDT
                           WHERE pdt.SoHD=c.SoHD AND dt.MaSKU=c.MaSKU AND pdt.TrangThai='HOAN_TAT'),0),
                   c.DonGia
            FROM ChiTietHoaDon c JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE c.SoHD=@id ORDER BY c.MaSKU
            """, ("@id", invoiceId));
        await using var detailReader = await detailCommand.ExecuteReaderAsync();
        while (await detailReader.ReadAsync())
        {
            var purchased = detailReader.GetInt32(5);
            var returned = detailReader.GetInt32(6);
            lines.Add(new(detailReader.GetString(0), detailReader.GetString(1), detailReader.GetString(2),
                detailReader.GetString(3), detailReader.GetString(4), purchased, returned,
                Math.Max(0, purchased - returned), detailReader.GetDecimal(7)));
        }

        var eligibility = GetReturnEligibility(sourceCode, createdAt, deliveryStatus, deliveredAt, paymentStatus);
        return new(id, createdAt.ToString("dd/MM/yyyy HH:mm"), customerName, phone,
            MapInvoiceSource(sourceCode), eligibility.StartDateText, eligibility.DeadlineText,
            eligibility.Allowed, eligibility.Message, lines);
    }

    public async Task<List<ReturnSummaryDto>> GetReturnsAsync()
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        var items = new List<ReturnSummaryDto>();
        await using var command = NewCommand(connection, null, """
            SELECT p.SoPhieuDT,p.NgayLap,p.SoHD,COALESCE(k.HoTen,'Không cung cấp'),
                   COALESCE(h.SoDienThoai_KH,''),p.HinhThucXuLy,s.TenSanPham,c.SoLuong,
                   p.SoTienHoan,p.SoTienThuThem
            FROM PhieuDoiTra p
            JOIN HoaDon h ON h.SoHD=p.SoHD
            LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            JOIN ChiTietDoiTra c ON c.SoPhieuDT=p.SoPhieuDT
            JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE p.TrangThai='HOAN_TAT'
            ORDER BY p.NgayLap DESC LIMIT 200
            """);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new(reader.GetString(0), reader.GetDateTime(1).ToString("dd/MM/yyyy HH:mm"),
                reader.GetString(2), reader.GetString(3), reader.GetString(4),
                MapReturnResolution(reader.GetString(5)), reader.GetString(6), reader.GetInt32(7),
                reader.GetDecimal(8), reader.GetDecimal(9)));
        return items;
    }

    public async Task<ReturnReceiptDto?> GetReturnAsync(string returnId)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = NewCommand(connection, null, """
            SELECT p.SoPhieuDT,p.NgayLap,p.SoHD,h.NgayLap,COALESCE(k.HoTen,'Không cung cấp'),
                   COALESCE(h.SoDienThoai_KH,''),nv.HoTen,p.NguonDon,c.LyDoTra,p.HinhThucXuLy,
                   c.MaSKU,sp.TenSanPham,sp.MauSac,sp.KichCo,c.SoLuong,c.DonGiaNhanLai,
                   c.ThanhTienNhanLai,c.MaSKUDoi,spm.TenSanPham,spm.MauSac,spm.KichCo,
                   c.DonGiaDoi,c.ThanhTienDoi,p.SoTienHoan,p.SoTienThuThem
            FROM PhieuDoiTra p
            JOIN HoaDon h ON h.SoHD=p.SoHD
            LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            JOIN NhanVien nv ON nv.MaNV=p.MaNV
            JOIN ChiTietDoiTra c ON c.SoPhieuDT=p.SoPhieuDT
            JOIN SanPham sp ON sp.MaSKU=c.MaSKU
            LEFT JOIN SanPham spm ON spm.MaSKU=c.MaSKUDoi
            WHERE p.SoPhieuDT=@id
            """, ("@id", returnId));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new(reader.GetString(0), reader.GetDateTime(1).ToString("dd/MM/yyyy HH:mm"),
            reader.GetString(2), reader.GetDateTime(3).ToString("dd/MM/yyyy HH:mm"),
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
            reader.GetString(8), MapReturnResolution(reader.GetString(9)), reader.GetString(10),
            reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetInt32(14),
            reader.GetDecimal(15), reader.GetDecimal(16), reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18), reader.IsDBNull(19) ? null : reader.GetString(19),
            reader.IsDBNull(20) ? null : reader.GetString(20), reader.GetDecimal(21), reader.GetDecimal(22),
            reader.GetDecimal(23), reader.GetDecimal(24));
    }

    // Tạo phiếu và cập nhật tồn kho trong cùng transaction để tránh đổi vượt số lượng mua.
    public async Task<ReturnReceiptDto> CreateReturnAsync(ReturnCreateDto input)
    {
        var allowedReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hàng bị hư", "Hàng bị lem màu", "Sản phẩm bị bung chỉ",
            "Sản phẩm không giống đơn đã đặt", "Giao sai sản phẩm", "Số lượng không đúng"
        };
        if (!allowedReasons.Contains(input.Reason.Trim()))
            throw new InvalidOperationException("Lỗi sản phẩm không nằm trong danh sách được đổi trả.");

        var resolution = input.Resolution.Trim().ToUpperInvariant();
        if (resolution is not ("DOI_SAN_PHAM" or "HOAN_TIEN"))
            throw new InvalidOperationException("Hình thức xử lý không hợp lệ.");
        if (resolution == "DOI_SAN_PHAM" && string.IsNullOrWhiteSpace(input.ReplacementSku))
            throw new InvalidOperationException("Vui lòng chọn sản phẩm đổi mới.");
        if (string.IsNullOrWhiteSpace(input.EmployeeId))
            throw new InvalidOperationException("Không xác định được nhân viên tiếp nhận.");

        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var tx = (NpgsqlTransaction)await connection.BeginTransactionAsync();

        string sourceCode;
        DateTime createdAt;
        string? deliveryStatus;
        DateTime? deliveredAt;
        string paymentStatus;
        string? customerId;
        await using (var invoiceCommand = NewCommand(connection, tx, """
            SELECT LoaiHoaDon,NgayLap,TrangThaiGiaoHang,NgayGiaoThanhCong,TrangThaiThanhToan,MaKH
            FROM HoaDon WHERE SoHD=@id
            """, ("@id", input.InvoiceId.Trim())))
        await using (var reader = await invoiceCommand.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new InvalidOperationException("Không tìm thấy hóa đơn.");
            sourceCode = reader.GetString(0);
            createdAt = reader.GetDateTime(1);
            deliveryStatus = reader.IsDBNull(2) ? null : reader.GetString(2);
            deliveredAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
            paymentStatus = reader.GetString(4);
            customerId = reader.IsDBNull(5) ? null : reader.GetString(5);
        }

        var eligibility = GetReturnEligibility(sourceCode, createdAt, deliveryStatus, deliveredAt, paymentStatus);
        if (!eligibility.Allowed) throw new InvalidOperationException(eligibility.Message);

        var employeeId = input.EmployeeId.Trim();
        if (await ScalarLongAsync(connection, tx,
                "SELECT COUNT(*) FROM NhanVien WHERE MaNV=@id AND TrangThai=TRUE", ("@id", employeeId)) == 0)
            throw new InvalidOperationException("Nhân viên tiếp nhận không tồn tại hoặc đã ngừng hoạt động.");

        string returnSku = input.ReturnSku.Trim().ToUpperInvariant();
        int purchasedQty;
        decimal returnUnitPrice;
        await using (var itemCommand = NewCommand(connection, tx, """
            SELECT c.SoLuong,c.DonGia
            FROM ChiTietHoaDon c
            WHERE c.SoHD=@invoice AND c.MaSKU=@sku
            """, ("@invoice", input.InvoiceId.Trim()), ("@sku", returnSku)))
        await using (var reader = await itemCommand.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new InvalidOperationException("Sản phẩm không thuộc hóa đơn này.");
            purchasedQty = reader.GetInt32(0);
            returnUnitPrice = reader.GetDecimal(1);
        }

        var returnedQty = await ScalarLongAsync(connection, tx, """
            SELECT COALESCE(SUM(c.SoLuong),0)
            FROM ChiTietDoiTra c JOIN PhieuDoiTra p ON p.SoPhieuDT=c.SoPhieuDT
            WHERE p.SoHD=@invoice AND c.MaSKU=@sku AND p.TrangThai='HOAN_TAT'
            """, ("@invoice", input.InvoiceId.Trim()), ("@sku", returnSku));
        if (input.Qty > purchasedQty - returnedQty)
            throw new InvalidOperationException("Số lượng đổi trả vượt quá số lượng còn lại của hóa đơn.");

        string? replacementSku = null;
        decimal replacementUnitPrice = 0;
        if (resolution == "DOI_SAN_PHAM")
        {
            replacementSku = input.ReplacementSku!.Trim().ToUpperInvariant();
            int replacementStock;
            await using var replacementCommand = NewCommand(connection, tx, """
                SELECT SoLuongTonKho,DonGia FROM SanPham
                WHERE MaSKU=@sku AND TrangThai=TRUE
                """, ("@sku", replacementSku));
            await using var reader = await replacementCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("Sản phẩm đổi mới không tồn tại.");
            replacementStock = reader.GetInt32(0);
            replacementUnitPrice = reader.GetDecimal(1);
            if (replacementStock < input.Qty)
                throw new InvalidOperationException("Sản phẩm đổi mới không đủ tồn kho.");
        }

        var returnValue = returnUnitPrice * input.Qty;
        var replacementValue = replacementUnitPrice * input.Qty;
        var refund = Math.Max(0, returnValue - replacementValue);
        var extra = Math.Max(0, replacementValue - returnValue);
        var dayPrefix = $"PDT-{DateTime.Now:yyyyMMdd}-";
        var sequence = await ScalarLongAsync(connection, tx, """
            SELECT COALESCE(MAX(NULLIF(RIGHT(SoPhieuDT,4),'')::int),0)+1
            FROM PhieuDoiTra WHERE SoPhieuDT LIKE @prefix
            """, ("@prefix", dayPrefix + "%"));
        var returnId = dayPrefix + sequence.ToString("0000", CultureInfo.InvariantCulture);

        await ExecuteAsync(connection, tx, """
            INSERT INTO PhieuDoiTra(SoPhieuDT,NgayLap,HinhThucXuLy,SoTienHoan,SoHD,MaNV,
                NguonDon,NgayMocTinh,GiaTriHangNhanLai,GiaTriHangDoiMoi,SoTienThuThem,TrangThai)
            VALUES(@id,CURRENT_TIMESTAMP,@resolution,@refund,@invoice,@employee,@source,@anchor,
                @returnValue,@replacementValue,@extra,'HOAN_TAT');
            INSERT INTO ChiTietDoiTra(SoPhieuDT,MaSKU,SoLuong,LoaiGiaoDich,TinhTrang,LyDoTra,
                MaSKUDoi,SoLuongDoi,DonGiaNhanLai,DonGiaDoi,ThanhTienNhanLai,ThanhTienDoi)
            VALUES(@id,@returnSku,@qty,@resolution,'Đã tiếp nhận',@reason,@replacementSku,
                @replacementQty,@returnPrice,@replacementPrice,@returnValue,@replacementValue);
            """, ("@id", returnId), ("@resolution", resolution), ("@refund", refund),
            ("@invoice", input.InvoiceId.Trim()), ("@employee", employeeId),
            ("@source", MapInvoiceSource(sourceCode)), ("@anchor", eligibility.AnchorDate),
            ("@returnValue", returnValue), ("@replacementValue", replacementValue), ("@extra", extra),
            ("@returnSku", returnSku), ("@qty", input.Qty), ("@reason", input.Reason.Trim()),
            ("@replacementSku", replacementSku), ("@replacementQty", replacementSku is null ? 0 : input.Qty),
            ("@returnPrice", returnUnitPrice), ("@replacementPrice", replacementUnitPrice));

        if (replacementSku is not null)
            await ExecuteAsync(connection, tx, """
                UPDATE SanPham SET SoLuongTonKho=SoLuongTonKho-@qty,SoLuong=SoLuong-@qty
                WHERE MaSKU=@sku
                """, ("@qty", input.Qty), ("@sku", replacementSku));

        if (customerId is not null)
        {
            var spendingDelta = replacementValue - returnValue;
            await ExecuteAsync(connection, tx, """
                UPDATE KhachHang
                SET TongChiTieu=CASE WHEN TongChiTieu+@delta<0 THEN 0 ELSE TongChiTieu+@delta END,
                    DiemTichLuy=FLOOR((CASE WHEN TongChiTieu+@delta<0 THEN 0 ELSE TongChiTieu+@delta END)/10000)::int,
                    HangThe=CASE
                        WHEN TongChiTieu+@delta>15000000 THEN 'Kim Cương'
                        WHEN TongChiTieu+@delta>5000000 THEN 'Vàng'
                        ELSE 'Bạc' END
                WHERE MaKH=@customer;
                """, ("@delta", spendingDelta), ("@customer", customerId));
        }

        await tx.CommitAsync();
        return await GetReturnAsync(returnId)
            ?? throw new InvalidOperationException("Đã lưu phiếu nhưng không đọc lại được dữ liệu.");
    }

    private static ReturnEligibility GetReturnEligibility(string sourceCode, DateTime createdAt,
        string? deliveryStatus, DateTime? deliveredAt, string paymentStatus)
    {
        if (!string.Equals(paymentStatus, "DA_THANH_TOAN", StringComparison.OrdinalIgnoreCase))
            return new(false, "Hóa đơn chưa thanh toán nên chưa thể đổi trả.", null, "-", "-");

        var isDelivery = sourceCode is "WEB" or "COD";
        if (isDelivery && (!string.Equals(deliveryStatus, "Hàng giao thành công", StringComparison.OrdinalIgnoreCase) || deliveredAt is null))
            return new(false, "Đơn giao hàng chưa ghi nhận giao thành công.", null, "-", "-");

        var anchor = isDelivery ? deliveredAt!.Value : createdAt;
        var deadline = anchor.AddDays(15);
        if (DateTime.Now > deadline)
            return new(false, "Hóa đơn đã quá thời hạn đổi trả 15 ngày.", anchor,
                anchor.ToString("dd/MM/yyyy HH:mm"), deadline.ToString("dd/MM/yyyy HH:mm"));
        return new(true, "Hóa đơn còn trong thời hạn đổi trả.", anchor,
            anchor.ToString("dd/MM/yyyy HH:mm"), deadline.ToString("dd/MM/yyyy HH:mm"));
    }

    private static string MapInvoiceSource(string sourceCode) => sourceCode switch
    {
        "WEB" => "Website",
        "COD" => "Giao hàng COD",
        _ => "Tại cửa hàng"
    };

    private static string MapReturnResolution(string value) => value switch
    {
        "DOI_SAN_PHAM" => "Đổi sản phẩm",
        "HOAN_TIEN" => "Hoàn tiền",
        _ => value
    };

    private sealed record ReturnEligibility(bool Allowed, string Message, DateTime? AnchorDate,
        string StartDateText, string DeadlineText);

    private static async Task<List<ProductTypeDto>> QueryProductTypesAsync(NpgsqlConnection connection)
    {
        var items = new List<ProductTypeDto>();
        await using var command = NewCommand(connection, null,
            "SELECT MaLoai,TenLoai FROM LoaiSanPham WHERE TrangThai=TRUE ORDER BY MaLoai");
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) items.Add(new(reader.GetString(0), reader.GetString(1)));
        return items;
    }

    private static async Task<List<ProductDto>> QueryProductsAsync(NpgsqlConnection connection)
    {
        var items = new List<ProductDto>();
        await using var command = NewCommand(connection, null, """
            SELECT sp.MaSP,sp.MaSKU,sp.TenSanPham,l.TenLoai,sp.MauSac,sp.KichCo,sp.DonViTinh,
                   sp.DonGia,sp.SoLuongTonKho,sp.HinhAnh
            FROM SanPham sp JOIN LoaiSanPham l ON l.MaLoai=sp.MaLoai
            WHERE sp.TrangThai=TRUE AND l.TrangThai=TRUE ORDER BY sp.MaSKU
            """);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        return items;
    }

    private static async Task<List<PartnerDto>> QueryPartnersAsync(NpgsqlConnection connection)
    {
        var items = new List<PartnerDto>();
        await using var command = NewCommand(connection, null, """
            SELECT 'Khách hàng',MaKH,HoTen,SoDienThoai,COALESCE(Email,''),COALESCE(DiaChi,''),HangThe,DiemTichLuy,TongChiTieu
            FROM KhachHang WHERE TrangThai=TRUE
            UNION ALL
            SELECT 'Nhà cung cấp',MaNSX,TenNSX,SoDienThoai,COALESCE(Email,''),COALESCE(DiaChi,''),COALESCE(MaSoThue,''),NULL,NULL
            FROM NhaSanXuat WHERE TrangThai=TRUE
            UNION ALL
            SELECT 'Nhân viên',MaNV,HoTen,SoDienThoai,COALESCE(Email,''),COALESCE(DiaChi,''),ChucVu,NULL,NULL
            FROM NhanVien WHERE TrangThai=TRUE ORDER BY 1,2
            """);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),
                reader.GetString(4),reader.GetString(5),reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),reader.IsDBNull(8) ? null : reader.GetDecimal(8)));
        return items;
    }

    private static async Task<List<PromotionDto>> QueryPromotionsAsync(NpgsqlConnection connection)
    {
        var items = new List<PromotionDto>();
        await using var command = NewCommand(connection, null,
            "SELECT MaKM,GiaTriGiam,GiaTriDonHangToiThieu,NgayBatDau,NgayKetThuc FROM KhuyenMai WHERE TrangThai=TRUE ORDER BY MaKM");
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) items.Add(new(reader.GetString(0),reader.GetDecimal(1),reader.GetDecimal(2),
            reader.GetDateTime(3).ToString("yyyy-MM-dd"),reader.GetDateTime(4).ToString("yyyy-MM-dd")));
        return items;
    }

    private static async Task<List<ImportRequestDto>> QueryImportRequestsAsync(NpgsqlConnection connection)
    {
        var items = new List<ImportRequestDto>();
        await using var command = NewCommand(connection, null, """
            SELECT p.SoPhieuNK,p.MaNSX,n.TenNSX,c.MaSKU,s.TenSanPham,c.SoLuong,nv.HoTen,p.NgayLap,p.TrangThai,COALESCE(c.GhiChu,'')
            FROM PhieuNhapKho p
            JOIN NhaSanXuat n ON n.MaNSX=p.MaNSX JOIN NhanVien nv ON nv.MaNV=p.MaNV
            JOIN ChiTietNhapKho c ON c.SoPhieuNK=p.SoPhieuNK JOIN SanPham s ON s.MaSKU=c.MaSKU
            ORDER BY p.NgayLap DESC
            """);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) items.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),
            reader.GetString(3),reader.GetString(4),reader.GetInt32(5),reader.GetString(6),
            reader.GetDateTime(7).ToString("dd/MM/yyyy HH:mm"),MapImportStatus(reader.GetString(8)),reader.GetString(9)));
        return items;
    }

    // Nạp lại doanh thu 30 ngày gần nhất để báo cáo không mất khi đóng chương trình.
    private static async Task<List<SaleHistoryDto>> QuerySalesHistoryAsync(NpgsqlConnection connection)
    {
        var rows = new List<(string Id, DateTime Date, string Customer, string Payment, string Source, decimal Total)>();
        await using (var command = NewCommand(connection, null, """
            SELECT h.SoHD,h.NgayLap,COALESCE(k.HoTen,'Khách lẻ'),h.PhuongThucThanhToan,
                   CASE WHEN UPPER(h.LoaiHoaDon) IN ('COD','WEB_COD') THEN 'COD'
                        WHEN UPPER(h.LoaiHoaDon) IN ('GIAO_HANG','WEB_DA_THANH_TOAN','WEB_THANH_TOAN') THEN 'Giao hàng'
                        ELSE 'POS' END,h.KhachPhaiTra
            FROM HoaDon h LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            WHERE h.TrangThaiThanhToan='DA_THANH_TOAN' AND h.NgayLap>=CURRENT_TIMESTAMP - INTERVAL '30 days'
            ORDER BY h.NgayLap
            """))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                rows.Add((reader.GetString(0),reader.GetDateTime(1),reader.GetString(2),
                    reader.GetString(3),reader.GetString(4),reader.GetDecimal(5)));

        var result = new List<SaleHistoryDto>();
        foreach (var row in rows)
        {
            var details = await QuerySaleItemsAsync(connection, row.Id);
            result.Add(new(row.Id,row.Date.ToString("yyyy-MM-dd"),row.Date.ToString("HH:mm"),row.Customer,
                row.Payment,row.Source,details.Sum(x => x.Qty),row.Total,details));
        }
        return result;
    }

    // Nạp các đơn đã thanh toán đang chờ giao từ POS hoặc website.
    private static async Task<List<DeliveryOrderDto>> QueryDeliveryOrdersAsync(NpgsqlConnection connection)
    {
        var rows = await QueryShipmentRowsAsync(connection, false);
        var result = new List<DeliveryOrderDto>();
        foreach (var row in rows)
        {
            var details = await QueryInvoiceItemsAsync(connection, row.Id);
            var customer = new InvoiceCustomerDto(row.Name, row.Phone);
            var receipt = new ReceiptStateDto(row.Id, customer, details, row.Subtotal, row.Discount, row.Vat,
                row.Total, row.Payment, row.Total, (int)Math.Floor(row.Total / 10000m), false);
            result.Add(new(row.Id, row.Name, row.Phone, row.Address, row.Note, row.ShippingFee, row.Status, row.Source, receipt));
        }
        return result;
    }

    // Nạp các đơn COD đang giao để nhân viên tiếp tục cập nhật ở lần mở sau.
    private static async Task<List<CodOrderDto>> QueryCodOrdersAsync(NpgsqlConnection connection)
    {
        var rows = await QueryShipmentRowsAsync(connection, true);
        var result = new List<CodOrderDto>();
        foreach (var row in rows)
        {
            var detailRows = await QueryInvoiceItemsAsync(connection, row.Id);
            var customer = new InvoiceCustomerDto(row.Name,row.Phone);
            var receipt = new ReceiptStateDto(row.Id,customer,detailRows,row.Subtotal,row.Discount,row.Vat,row.Total,
                row.Payment,0,(int)Math.Floor(row.Total/10000m),true);
            result.Add(new(row.Id,row.Name,row.Phone,row.Address,row.Note,row.Total,row.ShippingFee,row.Status,row.Source,receipt));
        }
        return result;
    }

    private sealed record ShipmentRow(string Id, string Name, string Phone, string Address, string Note, string Status,
        decimal Subtotal, decimal Discount, decimal Vat, decimal Total, string Payment, decimal ShippingFee, string Source);

    private static async Task<List<ShipmentRow>> QueryShipmentRowsAsync(NpgsqlConnection connection, bool cod)
    {
        var rows = new List<ShipmentRow>();
        var types = cod
            ? "('COD','WEB_COD')"
            : "('GIAO_HANG','WEB_DA_THANH_TOAN','WEB_THANH_TOAN')";
        await using var command = NewCommand(connection, null, $"""
            SELECT h.SoHD,COALESCE(NULLIF(h.TenNguoiNhan,''),k.HoTen,'Khách giao hàng'),
                   COALESCE(h.SoDienThoai_KH,''),COALESCE(h.DiaChiGiao,''),COALESCE(h.GhiChuGiaoHang,''),
                   COALESCE(h.TrangThaiGiaoHang,'Vừa xác nhận đơn'),h.TongCong,h.ChietKhau,h.ThueVAT,
                   h.KhachPhaiTra,h.PhuongThucThanhToan,COALESCE(h.PhiShip,0),
                   COALESCE(NULLIF(h.NguonDon,''),CASE WHEN h.SoHD LIKE 'WEB%' THEN 'WEB' ELSE 'POS' END)
            FROM HoaDon h LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            WHERE UPPER(h.LoaiHoaDon) IN {types}
              AND COALESCE(h.TrangThaiGiaoHang,'')<>'Hàng giao thành công'
            ORDER BY h.NgayLap DESC
            """);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),
                reader.GetString(4),reader.GetString(5),reader.GetDecimal(6),reader.GetDecimal(7),reader.GetDecimal(8),
                reader.GetDecimal(9),reader.GetString(10),reader.GetDecimal(11),reader.GetString(12)));
        return rows;
    }

    private static async Task<List<SaleItemDto>> QuerySaleItemsAsync(NpgsqlConnection connection, string invoiceId)
    {
        var items = new List<SaleItemDto>();
        await using var command = NewCommand(connection, null, """
            SELECT c.MaSKU,s.TenSanPham,c.SoLuong,c.ThanhTien
            FROM ChiTietHoaDon c JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE c.SoHD=@id ORDER BY c.MaSKU
            """, ("@id", invoiceId));
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new(reader.GetString(0),reader.GetString(1),reader.GetInt32(2),reader.GetDecimal(3)));
        return items;
    }

    private static async Task<List<InvoiceItemDto>> QueryInvoiceItemsAsync(NpgsqlConnection connection, string invoiceId)
    {
        var items = new List<InvoiceItemDto>();
        await using var command = NewCommand(connection, null, """
            SELECT c.MaSKU,s.TenSanPham,c.SoLuong,c.DonGia,c.ThanhTien
            FROM ChiTietHoaDon c JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE c.SoHD=@id ORDER BY c.MaSKU
            """, ("@id", invoiceId));
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new(reader.GetString(0),reader.GetString(1),reader.GetInt32(2),reader.GetDecimal(3),reader.GetDecimal(4)));
        return items;
    }

    private async Task<int> ExecuteStandaloneAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        return await ExecuteAsync(connection, null, sql, parameters);
    }

    private static NpgsqlCommand NewCommand(NpgsqlConnection connection, NpgsqlTransaction? tx, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction? tx, string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = NewCommand(connection, tx, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = NewCommand(connection, tx, sql, parameters);
        return await command.ExecuteScalarAsync();
    }

    private static async Task<string?> ScalarStringAsync(NpgsqlConnection c, NpgsqlTransaction tx, string sql,
        params (string Name, object? Value)[] p)
    {
        var value = await ScalarAsync(c, tx, sql, p);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection c, NpgsqlTransaction tx, string sql,
        params (string Name, object? Value)[] p) => Convert.ToInt64(await ScalarAsync(c, tx, sql, p), CultureInfo.InvariantCulture);

    private static async Task<decimal?> ScalarDecimalAsync(NpgsqlConnection c, NpgsqlTransaction tx, string sql,
        params (string Name, object? Value)[] p)
    {
        var value = await ScalarAsync(c, tx, sql, p);
        return value is null or DBNull ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static (string Name, object? Value)[] CommonPartnerParams(PartnerUpsertDto input) =>
    [
        ("@id", input.Id.Trim()),("@name", input.Name.Trim()),("@phone", input.Phone.Trim()),
        ("@email", DbValue(input.Email)),("@address", DbValue(input.Address)),("@role", DbValue(input.Role))
    ];

    private static object? DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static DateTime ParseDate(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture).Date;
    private static string MapImportStatus(string status) => status switch
    {
        "DA_DUYET" or "DA_NHAP" => "Đã duyệt",
        "TU_CHOI" or "HUY" => "Từ chối",
        _ => "Chờ duyệt"
    };
    private static string MapPermission(string? role) => role switch
    {
        "Quản lý" or "Cửa hàng trưởng" => "CHT",
        "Ca trưởng" or "Trưởng ca" => "TC",
        "Nhân viên kho" => "KHO",
        "Thu ngân" => "TN",
        _ => "TN"
    };
}




