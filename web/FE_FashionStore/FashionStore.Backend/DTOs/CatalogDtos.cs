using System.ComponentModel.DataAnnotations;

namespace FashionStore.Backend.DTOs;

public sealed class ProductQuery : IValidatableObject
{
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal? MinPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal? MaxPrice { get; set; }
    [StringLength(30)] public string? Size { get; set; }
    [StringLength(60)] public string? Color { get; set; }
    public bool InStock { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
            yield return new ValidationResult("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu.", new[] { nameof(MaxPrice) });
    }
    [StringLength(100)] public string? Q { get; set; }
    [StringLength(20)] public string? Category { get; set; }
    [RegularExpression("^(NAM|NU|PHU_KIEN)$")] public string? Audience { get; set; }
    [RegularExpression("^(new|price_asc|price_desc|name)$")] public string Sort { get; set; } = "new";
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 12;
}

public sealed record CategoryResponse(string Id, string Name);
public sealed record VariantResponse(string Sku, string Size, string Color, decimal Price, int Stock);
public sealed record ProductImageResponse(string? Sku, string Url);
public class ProductSummaryResponse
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public CategoryResponse Category { get; init; } = null!;
    public string? Audience { get; init; }
    public decimal Price { get; init; }
    public long Stock { get; init; }
    public string? ImageUrl { get; init; }
    public DateTime? CreatedAt { get; init; }
}
public sealed class ProductDetailResponse : ProductSummaryResponse
{
    public string? Description { get; init; }
    public IReadOnlyList<VariantResponse> Variants { get; init; } = [];
    public IReadOnlyList<ProductImageResponse> Images { get; init; } = [];
}
public sealed record ProductListResponse(IReadOnlyList<ProductSummaryResponse> Items, int Total, int Page, int PageSize)
{
    public bool SupportsAudience { get; init; } = true;
    public bool SupportsNewest { get; init; } = true;
}
