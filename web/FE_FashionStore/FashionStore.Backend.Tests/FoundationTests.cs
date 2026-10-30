using System.Net;
using System.Text.Json;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class FoundationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;
    public FoundationTests(ApiFactory factory) => client = factory.CreateClient();

    [Fact]
    public async Task SwaggerDocumentsProductEndpoints()
    {
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/products", out _));
        Assert.True(paths.TryGetProperty("/api/products/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/products/{id}/variants", out _));
        Assert.True(paths.TryGetProperty("/api/categories", out _));
        Assert.False(paths.TryGetProperty("/api/app-state", out _));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("sort=invalid")]
    [InlineData("audience=invalid")]
    [InlineData("category=123456789012345678901")]
    public async Task InvalidQueryIs400WithoutReadingDatabase(string query)
    {
        using var response = await client.GetAsync("/api/products?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OversizedSearchAndProductIdAreRejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/products?q=" + new string('x', 101))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/products/" + new string('x', 21))).StatusCode);
    }

    [Fact]
    public async Task SqlUnavailableIs503AndDoesNotLeakConnectionInformation()
    {
        using var response = await client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("127.0.0.1", body);
        Assert.DoesNotContain("SqlException", body);
        Assert.DoesNotContain("FashionStoreDB_Dev", body);
    }

    [Fact]
    public async Task LivenessAndSwaggerUiDoNotRequireSqlConnection()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }
}
