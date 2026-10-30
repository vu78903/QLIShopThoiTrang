using System.ComponentModel.DataAnnotations;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace FashionStore.Backend.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<ProductListResponse> Search([FromQuery] ProductQuery query, CancellationToken cancellationToken) =>
        catalog.SearchAsync(query, cancellationToken);

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDetailResponse>> Detail(
        [FromRoute, StringLength(20, MinimumLength = 1)] string id, CancellationToken cancellationToken)
    {
        var product = await catalog.GetDetailAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("{id}/variants")]
    public async Task<ActionResult<IReadOnlyList<VariantResponse>>> Variants(
        [FromRoute, StringLength(20, MinimumLength = 1)] string id, CancellationToken cancellationToken)
    {
        var variants = await catalog.GetVariantsAsync(id, cancellationToken);
        return variants is null ? NotFound() : Ok(variants);
    }
}
