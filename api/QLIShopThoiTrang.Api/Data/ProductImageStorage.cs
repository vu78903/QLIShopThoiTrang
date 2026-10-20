using System.Text.RegularExpressions;

namespace QLIShopThoiTrang.Api.Data;

// Lưu file ảnh sản phẩm trong project và chỉ trả đường dẫn để ghi vào database.
public sealed class ProductImageStorage(IWebHostEnvironment environment)
{
    private static readonly Regex DataUrlPattern = new(
        @"^data:image/(?<type>png|jpeg|webp);base64,(?<data>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<string?> SaveAsync(string sku, string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
            return null;

        var value = image.Trim();
        var match = DataUrlPattern.Match(value);
        if (!match.Success) return value;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(match.Groups["data"].Value);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Dữ liệu ảnh không hợp lệ.");
        }

        if (bytes.Length > 2 * 1024 * 1024)
            throw new InvalidOperationException("Ảnh không được lớn hơn 2 MB.");

        var extension = match.Groups["type"].Value.ToLowerInvariant() == "jpeg" ? ".jpg" : $".{match.Groups["type"].Value.ToLowerInvariant()}";
        var safeSku = Regex.Replace(sku.ToUpperInvariant(), @"[^A-Z0-9-]", "");
        var fileName = $"{safeSku}-{Guid.NewGuid():N}{extension}";
        var imageDirectory = Path.Combine(environment.ContentRootPath, "wwwroot", "hinh-anh-san-pham");
        Directory.CreateDirectory(imageDirectory);
        await File.WriteAllBytesAsync(Path.Combine(imageDirectory, fileName), bytes);

        return $"/hinh-anh-san-pham/{fileName}";
    }

    public void DeleteManagedFile(string? imagePath)
    {
        const string prefix = "/hinh-anh-san-pham/";
        if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return;

        var fileName = Path.GetFileName(imagePath);
        var fullPath = Path.Combine(environment.ContentRootPath, "wwwroot", "hinh-anh-san-pham", fileName);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }
}
