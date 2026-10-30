using FashionStore.Backend.DTOs;

namespace FashionStore.Backend.Services;

public interface ICatalogService
{
    Task<ProductListResponse> SearchAsync(ProductQuery query, CancellationToken cancellationToken);
    Task<List<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<ProductDetailResponse?> GetDetailAsync(string id, CancellationToken cancellationToken);
    Task<List<VariantResponse>?> GetVariantsAsync(string id, CancellationToken cancellationToken);
}
