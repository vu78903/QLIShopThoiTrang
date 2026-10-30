using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FashionStore.Backend.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string? connection;
    public ApiFactory() { }
    internal ApiFactory(string connection) => this.connection = connection;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FashionStore"] = connection ??
                "Server=127.0.0.1,1;Database=FashionStoreDB_Dev;Integrated Security=True;Encrypt=True;Connect Timeout=1"
        }));
    }
}
