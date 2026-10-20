using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace QLIShopThoiTrang.Api.Data;

// Đưa ảnh sản phẩm lên Supabase Storage và trả URL để lưu vào bảng SanPham.
public sealed class ProductImageStorage(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private const string BucketName = "product-images";
    private static readonly Regex DataUrlPattern = new(
        @"^data:image/(?<type>png|jpeg|webp);base64,(?<data>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _supabaseUrl = (
        Environment.GetEnvironmentVariable("SUPABASE_URL")
        ?? configuration["Supabase:Url"]
        ?? string.Empty).TrimEnd('/');

    private readonly string _serviceRoleKey =
        Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY")
        ?? configuration["Supabase:ServiceRoleKey"]
        ?? string.Empty;

    public async Task<string?> SaveAsync(string sku, string? image)
    {
        if (string.IsNullOrWhiteSpace(image)) return null;

        var value = image.Trim();
        var match = DataUrlPattern.Match(value);
        if (!match.Success) return value;

        EnsureConfigured();

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

        await EnsureBucketAsync();

        var type = match.Groups["type"].Value.ToLowerInvariant();
        var extension = type == "jpeg" ? "jpg" : type;
        var contentType = type == "jpeg" ? "image/jpeg" : $"image/{type}";
        var safeSku = Regex.Replace(sku.ToUpperInvariant(), @"[^A-Z0-9-]", "");
        var objectPath = $"{safeSku}/{Guid.NewGuid():N}.{extension}";

        using var request = CreateRequest(HttpMethod.Post,
            $"/storage/v1/object/{BucketName}/{objectPath}");
        request.Headers.Add("x-upsert", "false");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await httpClientFactory.CreateClient().SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Supabase Storage từ chối ảnh: " + await response.Content.ReadAsStringAsync());

        return $"{_supabaseUrl}/storage/v1/object/public/{BucketName}/{objectPath}";
    }

    public async Task DeleteManagedFileAsync(string? imageUrl)
    {
        var objectPath = GetManagedObjectPath(imageUrl);
        if (objectPath is null) return;

        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Delete,
            $"/storage/v1/object/{BucketName}/{objectPath}");
        using var response = await httpClientFactory.CreateClient().SendAsync(request);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            throw new InvalidOperationException("Không xóa được ảnh cũ trên Supabase Storage: " +
                                                await response.Content.ReadAsStringAsync());
    }

    private async Task EnsureBucketAsync()
    {
        using var getRequest = CreateRequest(HttpMethod.Get, $"/storage/v1/bucket/{BucketName}");
        using var getResponse = await httpClientFactory.CreateClient().SendAsync(getRequest);
        if (getResponse.IsSuccessStatusCode) return;
        if (getResponse.StatusCode != HttpStatusCode.NotFound)
            throw new InvalidOperationException("Không kiểm tra được bucket Supabase: " +
                                                await getResponse.Content.ReadAsStringAsync());

        using var createRequest = CreateRequest(HttpMethod.Post, "/storage/v1/bucket");
        createRequest.Content = JsonContent.Create(new
        {
            id = BucketName,
            name = BucketName,
            @public = true,
            file_size_limit = 2 * 1024 * 1024,
            allowed_mime_types = new[] { "image/png", "image/jpeg", "image/webp" }
        });
        using var createResponse = await httpClientFactory.CreateClient().SendAsync(createRequest);
        if (!createResponse.IsSuccessStatusCode && createResponse.StatusCode != HttpStatusCode.Conflict)
            throw new InvalidOperationException("Không tạo được bucket product-images: " +
                                                await createResponse.Content.ReadAsStringAsync());
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, _supabaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        request.Headers.Add("apikey", _serviceRoleKey);
        return request;
    }

    private string? GetManagedObjectPath(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(_supabaseUrl)) return null;
        var prefix = $"{_supabaseUrl}/storage/v1/object/public/{BucketName}/";
        return imageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? imageUrl[prefix.Length..]
            : null;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_supabaseUrl) || string.IsNullOrWhiteSpace(_serviceRoleKey))
            throw new InvalidOperationException(
                "Thiếu SUPABASE_URL hoặc SUPABASE_SERVICE_ROLE_KEY. Hãy đặt hai biến môi trường này rồi mở lại ứng dụng.");
    }
}
