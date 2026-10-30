using System.Net;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class UnconfiguredPublicEndpointsTests
{
    [Fact] public async Task PublicHealthAndSwaggerAndAuthenticationChallengeDoNotResolveSql()
    {
        await using var factory=new ApiFactory("");using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync("/health/ready")).StatusCode);
    }
}
