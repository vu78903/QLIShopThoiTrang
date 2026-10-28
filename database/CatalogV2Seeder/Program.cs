using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("YODYSTORE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Thiếu YODYSTORE_CONNECTION_STRING.");
var specs = Catalog.Build();
var desired = Catalog.Expand(specs).ToList();
var imagePlan = BuildImagePlan(Environment.GetEnvironmentVariable("SUPABASE_URL")?.TrimEnd('/')
    ?? "https://oagcfzcslkkiokbmkrlg.supabase.co");
if (desired.Count != 990) throw new InvalidOperationException($"Catalog phải có 990 SKU, hiện có {desired.Count}.");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

var current = new List<ExistingProduct>();
await using (var command = new NpgsqlCommand("SELECT masp,masku,mausac,kichco,hinhanh FROM sanpham", connection))
await using (var reader = await command.ExecuteReaderAsync())
    while (await reader.ReadAsync()) current.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
        reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));

var existingByVariant = current.GroupBy(x => VariantKey(x.ProductId, x.Color, x.Size), StringComparer.OrdinalIgnoreCase)
    .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
var desiredKeys = desired.Select(x => VariantKey(x.ProductId, x.Color, x.Size)).ToHashSet(StringComparer.OrdinalIgnoreCase);
var obsoleteSkus = current.Where(x => !desiredKeys.Contains(VariantKey(x.ProductId, x.Color, x.Size)))
    .Select(x => x.Sku).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

if (args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        currentVariants = current.Count,
        plannedProducts = specs.Sum(x => x.Products.Length),
        plannedVariants = desired.Count,
        variantsToKeep = current.Count - obsoleteSkus.Length,
        variantsToHardDelete = obsoleteSkus.Length,
        variantsToInsert = desired.Count - desired.Count(x => existingByVariant.ContainsKey(VariantKey(x.ProductId, x.Color, x.Size)))
    }));
    return;
}

Console.WriteLine($"Đã lập kế hoạch: giữ {current.Count - obsoleteSkus.Length}, xóa {obsoleteSkus.Length}, tổng mới {desired.Count} SKU.");
string? stripedImageUrl = null;
try
{
    stripedImageUrl = await UploadStripedImageAsync();
    Console.WriteLine(stripedImageUrl is null ? "Bỏ qua ảnh áo thun sọc." : "Đã tải ảnh áo thun sọc.");
}
catch (Exception error)
{
    Console.WriteLine("Không tải được ảnh áo thun sọc, tiếp tục để trống ảnh: " + error.Message);
}
var usedSkus = current.Where(x => !obsoleteSkus.Contains(x.Sku, StringComparer.OrdinalIgnoreCase))
    .Select(x => x.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
var random = new Random(20261027);

await using var tx = await connection.BeginTransactionAsync();
try
{
    Console.WriteLine("Bắt đầu giao dịch catalog.");
    await ExecuteAsync(connection, tx, "CREATE TEMP TABLE obsolete_sku(masku varchar(40) PRIMARY KEY) ON COMMIT DROP");
    foreach (var sku in obsoleteSkus)
        await ExecuteAsync(connection, tx, "INSERT INTO obsolete_sku(masku) VALUES(@sku)", ("sku", sku));
    Console.WriteLine("Đã lập danh sách SKU dư trong transaction.");

    // Chỉ xóa những dòng tham chiếu trực tiếp SKU dư; không xóa chứng từ còn SKU hợp lệ.
    await ExecuteAsync(connection, tx, """
        DO $$ BEGIN
          IF to_regclass('webstore.cart_line') IS NOT NULL THEN
            EXECUTE 'DELETE FROM webstore.cart_line WHERE masku IN (SELECT masku FROM obsolete_sku)';
          END IF;
        END $$;
        DELETE FROM chitietdoitra WHERE masku IN (SELECT masku FROM obsolete_sku)
                                     OR maskudoi IN (SELECT masku FROM obsolete_sku);
        DELETE FROM chitiethoadon WHERE masku IN (SELECT masku FROM obsolete_sku);
        DELETE FROM chitietnhapkho WHERE masku IN (SELECT masku FROM obsolete_sku);
        DELETE FROM chitietbaocaodoanhthu WHERE masku IN (SELECT masku FROM obsolete_sku);
        DELETE FROM bienbanxulyhangloi WHERE masku IN (SELECT masku FROM obsolete_sku);
        DELETE FROM phieudoitra p WHERE NOT EXISTS (SELECT 1 FROM chitietdoitra d WHERE d.sophieudt=p.sophieudt);
        DELETE FROM hoadon h WHERE NOT EXISTS (SELECT 1 FROM chitiethoadon d WHERE d.sohd=h.sohd)
          AND NOT EXISTS (SELECT 1 FROM phieudoitra p WHERE p.sohd=h.sohd);
        DELETE FROM phieunhapkho p WHERE NOT EXISTS (SELECT 1 FROM chitietnhapkho d WHERE d.sophieunk=p.sophieunk);
        DELETE FROM baocaodoanhthu b WHERE NOT EXISTS (SELECT 1 FROM chitietbaocaodoanhthu d WHERE d.mabaocao=b.mabaocao);
        DELETE FROM sanpham WHERE masku IN (SELECT masku FROM obsolete_sku);
        UPDATE loaisanpham SET trangthai=FALSE;
        UPDATE loaisanpham SET tenloai='Loại cũ ' || maloai
        WHERE maloai NOT IN ('LSP-101','LSP-102','LSP-103','LSP-104','LSP-105','LSP-106',
                             'LSP-107','LSP-108','LSP-109','LSP-110','LSP-111','LSP-112',
                             'LSP-113','LSP-114','LSP-115','LSP-116','LSP-117','LSP-118');
        """);
    Console.WriteLine("Đã xóa SKU dư và tham chiếu trực tiếp.");

    foreach (var type in specs)
        await ExecuteAsync(connection, tx, """
            INSERT INTO loaisanpham(maloai,tenloai,mota,trangthai)
            VALUES(@id,@name,@description,TRUE)
            ON CONFLICT(maloai) DO UPDATE SET tenloai=EXCLUDED.tenloai,mota=EXCLUDED.mota,trangthai=TRUE
            """, ("id", type.Id), ("name", type.Name), ("description", $"Danh mục {Catalog.AudienceLabel(type.Audience)}"));
    Console.WriteLine("Đã cập nhật 18 loại sản phẩm.");

    var retained = 0;
    var inserted = 0;
    var imageVariants = 0;
    foreach (var item in desired)
    {
        var key = VariantKey(item.ProductId, item.Color, item.Size);
        var image = imagePlan.GetValueOrDefault(ImageKey(item.ProductId, item.Color));
        var supplier = SupplierIdFor(item.TypeId);
        if (item.ProductId == "SP-001" && item.Color == "Đen" && !string.IsNullOrWhiteSpace(stripedImageUrl))
            image = stripedImageUrl;
        if (!string.IsNullOrWhiteSpace(image)) imageVariants++;

        if (existingByVariant.TryGetValue(key, out var existing) && !obsoleteSkus.Contains(existing.Sku, StringComparer.OrdinalIgnoreCase))
        {
            await ExecuteAsync(connection, tx, """
                UPDATE sanpham SET maloai=@type,tensanpham=@name,danhmuc=@audience,mansx=@supplier,soluong=@stock,
                    trangthai=TRUE,dongia=@price,soluongtonkho=@stock,donvitinh='Cái',hinhanh=@image
                WHERE masku=@sku
                """, ("type", item.TypeId), ("name", item.Name), ("audience", item.Audience),
                ("supplier", supplier), ("stock", random.Next(5, 41)), ("price", item.Price), ("image", image ?? (object)DBNull.Value),
                ("sku", existing.Sku));
            retained++;
            continue;
        }

        var sku = Catalog.UniqueSku(item, usedSkus);
        usedSkus.Add(sku);
        await ExecuteAsync(connection, tx, """
            INSERT INTO sanpham(masp,masku,maloai,tensanpham,danhmuc,mansx,soluong,kichco,mausac,trangthai,
                               dongia,soluongtonkho,donvitinh,hinhanh)
            VALUES(@productId,@sku,@type,@name,@audience,@supplier,@stock,@size,@color,TRUE,
                   @price,@stock,'Cái',@image)
            """, ("productId", item.ProductId), ("sku", sku), ("type", item.TypeId), ("name", item.Name),
            ("audience", item.Audience), ("supplier", supplier), ("stock", random.Next(5, 41)), ("size", item.Size),
            ("color", item.Color), ("price", item.Price), ("image", image ?? (object)DBNull.Value));
        inserted++;
        if ((retained + inserted) % 100 == 0) Console.WriteLine($"Đã ghi {retained + inserted}/{desired.Count} SKU.");
    }

    await ExecuteAsync(connection, tx, """
        DELETE FROM loaisanpham l
        WHERE l.trangthai=FALSE AND NOT EXISTS (SELECT 1 FROM sanpham s WHERE s.maloai=l.maloai)
        """);

    await tx.CommitAsync();
    Console.WriteLine("Đã commit catalog.");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        types = specs.Count, products = specs.Sum(x => x.Products.Length), variants = desired.Count,
        retainedVariants = retained, insertedVariants = inserted, hardDeletedVariants = obsoleteSkus.Length,
        imageVariants
    }));
}
catch (Exception error)
{
    await tx.RollbackAsync();
    Console.Error.WriteLine("Seeder rollback: " + error);
    Environment.ExitCode = 1;
}

static string VariantKey(string productId, string color, string size) => $"{productId}|{color.Trim()}|{size.Trim()}";
static string ImageKey(string productId, string color) => $"{productId}|{color.Trim()}";

static string SupplierIdFor(string typeId) => typeId switch
{
    "LSP-102" or "LSP-108" or "LSP-110" => "NCC-004", // Việt Tiến: quần tây, áo vest
    "LSP-101" or "LSP-109" => "NCC-001",               // Chợ An Đông: áo thun nam/nữ
    "LSP-105" or "LSP-115" or "LSP-117" or "LSP-118" => "NCC-002", // Thành Công: đồ lót, phụ kiện
    "LSP-103" or "LSP-104" or "LSP-106" or "LSP-107" => "NCC-003", // Dony: đồ nam còn lại
    _ => "NCC-005" // Nhà Bè: đồ nữ còn lại
};

// Chỉ giữ những ảnh đã được kiểm tra trực quan là đúng kiểu sản phẩm và đúng màu.
// Các ảnh cũ không khớp với bất kỳ tên sản phẩm hiện tại nào chủ động để trống.
static Dictionary<string, string> BuildImagePlan(string storageBase)
{
    string CatalogImage(string oldProductId, string fileName) =>
        $"{storageBase}/storage/v1/object/public/product-images/catalog/{oldProductId}/{fileName}.png";

    return new(StringComparer.OrdinalIgnoreCase)
    {
        [ImageKey("SP-001", "Đen")] = $"{storageBase}/storage/v1/object/public/product-images/catalog-v2/SP-001/den.png",
        [ImageKey("SP-002", "Đỏ")] = CatalogImage("SP-001", "do"),
        [ImageKey("SP-002", "Trắng")] = CatalogImage("SP-001", "trang"),
        [ImageKey("SP-007", "Đen")] = CatalogImage("SP-007", "den"),
        [ImageKey("SP-011", "Đen")] = CatalogImage("SP-008", "den"),
        [ImageKey("SP-011", "Trắng")] = CatalogImage("SP-008", "trang"),
        [ImageKey("SP-016", "Đen")] = CatalogImage("SP-005", "den"),
        [ImageKey("SP-016", "Đỏ")] = CatalogImage("SP-005", "do"),
        [ImageKey("SP-016", "Trắng")] = CatalogImage("SP-005", "trang"),
        [ImageKey("SP-019", "Đen")] = CatalogImage("SP-022", "den"),
        [ImageKey("SP-019", "Trắng")] = CatalogImage("SP-022", "trang"),
        [ImageKey("SP-026", "Đen")] = CatalogImage("SP-004", "den"),
        [ImageKey("SP-026", "Đỏ")] = CatalogImage("SP-004", "do"),
        [ImageKey("SP-057", "Đen")] = CatalogImage("SP-020", "den"),
        [ImageKey("SP-057", "Đỏ")] = CatalogImage("SP-020", "do"),
        [ImageKey("SP-057", "Trắng")] = CatalogImage("SP-020", "trang"),
        [ImageKey("SP-060", "Đen")] = CatalogImage("SP-019", "den"),
        [ImageKey("SP-060", "Đỏ")] = CatalogImage("SP-019", "do"),
        [ImageKey("SP-060", "Trắng")] = CatalogImage("SP-019", "trang"),
        [ImageKey("SP-061", "Đen")] = CatalogImage("SP-017", "den"),
        [ImageKey("SP-061", "Đỏ")] = CatalogImage("SP-017", "do"),
        [ImageKey("SP-061", "Trắng")] = CatalogImage("SP-017", "trang"),
        [ImageKey("SP-062", "Đen")] = CatalogImage("SP-018", "den"),
        [ImageKey("SP-062", "Đỏ")] = CatalogImage("SP-018", "do"),
        [ImageKey("SP-062", "Trắng")] = CatalogImage("SP-018", "trang")
    };
}

async Task<string?> UploadStripedImageAsync()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var path = Path.Combine(root, "winform", "qlicuahang", "assets", "catalog-v2", "SP-001", "den.png");
    var url = Environment.GetEnvironmentVariable("SUPABASE_URL")?.TrimEnd('/');
    var key = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
    if (!File.Exists(path) || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)) return null;
    using var http = new HttpClient();
    http.Timeout = TimeSpan.FromSeconds(15);
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    http.DefaultRequestHeaders.Add("apikey", key);
    http.DefaultRequestHeaders.Add("x-upsert", "true");
    await using var file = File.OpenRead(path);
    using var content = new StreamContent(file);
    content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
    using var response = await http.PostAsync($"{url}/storage/v1/object/product-images/catalog-v2/SP-001/den.png", content);
    response.EnsureSuccessStatusCode();
    return $"{url}/storage/v1/object/public/product-images/catalog-v2/SP-001/den.png";
}

static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction tx, string sql,
    params (string Name, object Value)[] parameters)
{
    await using var command = new NpgsqlCommand(sql, connection, tx);
    command.CommandTimeout = 120;
    foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
    await command.ExecuteNonQueryAsync();
}

internal sealed record ExistingProduct(string ProductId, string Sku, string Color, string Size, string? Image);
internal sealed record TypeSpec(string Id, string Name, string Audience, decimal BasePrice, string[] Products);
internal sealed record VariantSpec(string ProductId, string TypeId, string Name, string Audience, decimal Price, string Color, string Size);

internal static class Catalog
{
    public static List<TypeSpec> Build() =>
    [
        new("LSP-101", "Áo thun nam", "NAM", 199_000, ["Áo thun sọc nam", "Áo thun trơn nam", "Áo thun nam tay dài", "Áo thun nam cổ V", "Áo thun nam oversize"]),
        new("LSP-102", "Quần tây nam", "NAM", 499_000, ["Quần tây nam slim fit", "Quần tây nam ống đứng", "Quần tây nam xếp ly", "Quần tây nam co giãn", "Quần tây nam công sở"]),
        new("LSP-103", "Quần jeans nam", "NAM", 529_000, ["Quần jeans nam ống đứng", "Quần jeans nam slim fit", "Quần jeans nam rách gối", "Quần jeans nam ống rộng", "Quần jeans nam wash bạc"]),
        new("LSP-104", "Áo hoodie nam", "NAM", 449_000, ["Áo hoodie nam trơn", "Áo hoodie nam khóa kéo", "Áo hoodie nam phối màu", "Áo hoodie nam nỉ bông", "Áo hoodie nam oversize"]),
        new("LSP-105", "Đồ lót nam", "NAM", 129_000, ["Boxer nam cotton", "Boxer nam modal", "Boxer nam thể thao", "Quần lót nam brief", "Boxer nam thoáng khí"]),
        new("LSP-106", "Áo khoác nam", "NAM", 599_000, ["Áo khoác gió nam", "Áo khoác bomber nam", "Áo khoác denim nam", "Áo khoác dù nam", "Áo khoác túi hộp nam"]),
        new("LSP-107", "Đồ thể thao nam", "NAM", 399_000, ["Bộ thể thao nam chạy bộ", "Bộ thể thao nam gym", "Bộ thể thao nam jogger", "Bộ thể thao nam tennis", "Bộ thể thao nam training"]),
        new("LSP-108", "Áo vest nam", "NAM", 999_000, ["Áo vest nam cổ điển", "Áo vest nam slim fit", "Áo vest nam tuxedo", "Áo vest nam kẻ caro", "Áo vest nam hai hàng khuy"]),
        new("LSP-109", "Áo thun baby tee nữ", "NU", 179_000, ["Baby tee nữ trơn", "Baby tee nữ gân", "Baby tee nữ họa tiết", "Baby tee nữ crop", "Baby tee nữ tay dài"]),
        new("LSP-110", "Quần tây nữ", "NU", 449_000, ["Quần tây nữ ống rộng", "Quần tây nữ slim fit", "Quần tây nữ xếp ly", "Quần tây nữ công sở", "Quần tây nữ lưng cao"]),
        new("LSP-111", "Quần jeans nữ", "NU", 499_000, ["Quần jeans nữ lưng cao", "Quần jeans nữ ống đứng", "Quần jeans nữ ống rộng", "Quần jeans nữ ống loe", "Quần jeans nữ rách nhẹ"]),
        new("LSP-112", "Đầm", "NU", 499_000, ["Đầm nữ suông", "Đầm nữ maxi", "Đầm nữ ôm body", "Đầm nữ quấn", "Đầm nữ xếp ly"]),
        new("LSP-113", "Váy", "NU", 349_000, ["Váy nữ chữ A", "Váy nữ maxi", "Váy nữ xếp ly", "Váy nữ bút chì", "Váy nữ denim"]),
        new("LSP-114", "Áo khoác nữ", "NU", 599_000, ["Áo khoác lông nữ", "Áo khoác blazer nữ", "Áo khoác cardigan nữ", "Áo khoác bomber nữ", "Áo khoác trench coat nữ"]),
        new("LSP-115", "Đồ lót nữ", "NU", 149_000, ["Áo lót nữ cotton", "Áo lót nữ không đường may", "Quần lót nữ cotton", "Quần lót nữ không đường may", "Bộ đồ ngủ nữ"]),
        new("LSP-116", "Đồ thể thao nữ", "NU", 399_000, ["Bộ thể thao nữ yoga", "Bộ thể thao nữ chạy bộ", "Bộ thể thao nữ tennis", "Bộ thể thao nữ gym", "Bộ thể thao nữ đạp xe"]),
        new("LSP-117", "Nón", "PHU_KIEN", 149_000, ["Nón lưỡi trai", "Nón bucket", "Nón len", "Nón vành rộng", "Nón beret"]),
        new("LSP-118", "Vớ", "PHU_KIEN", 59_000, ["Vớ cổ trung", "Vớ cổ ngắn", "Vớ thể thao", "Vớ cổ cao", "Vớ lười"])
    ];

    public static IEnumerable<VariantSpec> Expand(List<TypeSpec> types)
    {
        var number = 1;
        foreach (var type in types)
            for (var productIndex = 0; productIndex < type.Products.Length; productIndex++)
            {
                var productId = $"SP-{number++:000}";
                var price = type.BasePrice + productIndex * PriceStep(type.Name);
                foreach (var color in Colors(type.Name))
                    foreach (var size in Sizes(type.Name))
                        yield return new(productId, type.Id, type.Products[productIndex], type.Audience, price, color, size);
            }
    }

    public static string UniqueSku(VariantSpec item, HashSet<string> used)
    {
        var sequence = Regex.Match(item.ProductId, @"\d+$").Value;
        var sku = $"{ProductCode(item.Name)}-{sequence}-{ColorCode(item.Color)}-{SizeCode(item.Size)}";
        if (sku.Length > 20) sku = sku[..20].TrimEnd('-');
        var original = sku;
        for (var suffix = 2; used.Contains(sku); suffix++)
        {
            var tail = $"-{suffix}";
            sku = original[..Math.Min(original.Length, 20 - tail.Length)].TrimEnd('-') + tail;
        }
        return sku;
    }

    private static string[] Colors(string type) => type switch
    {
        "Quần jeans nam" or "Quần jeans nữ" => ["Trắng", "Xanh jeans", "Đen"],
        "Đồ lót nam" => ["Trắng", "Đen", "Xám"], "Đồ lót nữ" => ["Trắng", "Đen", "Be"],
        _ => ["Trắng", "Đen", "Đỏ"]
    };
    private static string[] Sizes(string type) => type switch
    {
        "Quần tây nam" or "Quần jeans nam" or "Quần tây nữ" or "Quần jeans nữ" => ["28", "30", "32", "34"],
        "Nón" or "Vớ" => ["Free size"], _ => ["S", "M", "L", "XL"]
    };
    private static decimal PriceStep(string type) => type switch
    { "Vớ" => 15_000m, "Đồ lót nam" or "Đồ lót nữ" or "Nón" => 30_000m, _ => 50_000m };
    private static string ProductCode(string name)
    {
        var text = Ascii(name);
        if (Regex.IsMatch(text, @"QUAN.*JEANS?")) return "QJ";
        if (Regex.IsMatch(text, @"QUAN.*TAY")) return "QT";
        (string Pattern, string Code)[] rules = [("HOODIE", "HD"), ("AO THUN", "AT"), ("BABY TEE", "BT"),
            ("AO KHOAC", "AK"), ("AO VEST", "AV"), ("BOXER", "BX"), ("QUAN LOT", "QL"),
            ("AO LOT", "AL"), ("BO THE THAO", "BTT"), ("BO DO NGU", "BDN"), ("VAY", "VAY"),
            ("DAM", "DAM"), ("NON", "NON"), ("VO", "VO")];
        foreach (var (pattern, code) in rules) if (text.Contains(pattern)) return code;
        var initials = string.Concat(Regex.Split(text, @"[^A-Z0-9]+").Where(x => x.Length > 0).Select(x => x[0]));
        return string.IsNullOrWhiteSpace(initials) ? "SP" : initials[..Math.Min(initials.Length, 4)];
    }
    private static string ColorCode(string color) => Ascii(color) switch
    { "TRANG" => "TRANG", "DEN" => "DEN", "DO" => "DO", "XAM" => "XAM", "BE" => "BE", "XANH JEANS" => "XANHJ", var x => Part(x, 7) };
    private static string SizeCode(string size) => Ascii(size) == "FREE SIZE" ? "FS" : Part(size, 4);
    private static string Part(string value, int max) { var part = Regex.Replace(Ascii(value), @"[^A-Z0-9]+", ""); return part.Length == 0 ? "SP" : part[..Math.Min(part.Length, max)]; }
    private static string Ascii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).ToUpperInvariant().Replace('Đ', 'D').Trim();
    }
    public static string AudienceLabel(string value) => value switch { "NAM" => "Nam", "NU" => "Nữ", _ => "Phụ kiện" };
}
