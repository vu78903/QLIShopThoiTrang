using FashionStore.Backend.DTOs;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace FashionStore.Backend.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<List<CategoryResponse>> List(CancellationToken cancellationToken) => catalog.GetCategoriesAsync(cancellationToken);
}
