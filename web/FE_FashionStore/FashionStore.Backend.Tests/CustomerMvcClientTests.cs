using System.Net;
using System.Text;
using FashionStore.Web.Models;
using FashionStore.Web.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class CustomerMvcClientTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public readonly List<(string? Token,string Method,string Path,JObject? Body)> Requests=[];
        public HttpStatusCode Status=HttpStatusCode.OK;
        public string? ResponseBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var json=request.Content==null?null:await request.Content.ReadAsStringAsync(ct);
            lock(Requests)Requests.Add((request.Headers.Authorization?.Parameter,request.Method.Method,request.RequestUri!.AbsolutePath,json==null?null:JObject.Parse(json)));
            var body=ResponseBody ?? (Status!=HttpStatusCode.OK?"{\"title\":\"Request rejected\"}":request.RequestUri!.AbsolutePath=="/api/account/me"
                ?"{\"id\":\"OWNER\",\"userName\":\"customer\",\"rowVersion\":\"AQIDBAUGBwg=\"}"
                :"{\"items\":[],\"totalQuantity\":0,\"subtotal\":0,\"canCheckout\":false}");
            return new HttpResponseMessage(Status){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        }
    }
    [Fact] public async Task ConcurrentCustomersUsePerRequestTokensAndNeverChangeSharedHeaders()
    {
        var handler=new CaptureHandler();using var http=new HttpClient(handler);
        var api=new CustomerApiClient(http,"http://backend.test/");
        await Task.WhenAll(api.CartAsync("token-a"),api.CartAsync("token-b"));
        Assert.Contains(handler.Requests,r=>r.Token=="token-a");Assert.Contains(handler.Requests,r=>r.Token=="token-b");
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }
    [Fact] public async Task ProfilePayloadDoesNotSendIdPointsMembershipOrUsername()
    {
        var handler=new CaptureHandler();using var http=new HttpClient(handler);
        var api=new CustomerApiClient(http,"http://backend.test/");
        await api.UpdateAsync("token",new CustomerProfile{Id="OTHER",UserName="admin",Points=999,Membership="Kim Cương",FullName="Customer",Phone="0900000001",Email="",Address="",RowVersion="AQIDBAUGBwg="});
        var body=Assert.Single(handler.Requests).Body!;
        Assert.Equal(new[]{"Address","Email","FullName","Phone","ReceivePromotions","RowVersion"},body.Properties().Select(p=>p.Name).OrderBy(n=>n));
        Assert.Equal(JTokenType.Null,body["Email"]!.Type);Assert.Equal(JTokenType.Null,body["Address"]!.Type);
    }
    [Fact] public async Task CartSendsOnlySkuAndQuantityAndEscapesSkuInPath()
    {
        var handler=new CaptureHandler();using var http=new HttpClient(handler);
        var api=new CustomerApiClient(http,"http://backend.test/");
        await api.AddAsync("token","SKU + đỏ",2);
        Assert.Equal(new[]{"Quantity","Sku"},handler.Requests[0].Body!.Properties().Select(p=>p.Name).OrderBy(n=>n));
        await api.SetAsync("token","SKU + đỏ",1);
        Assert.Equal("PUT",handler.Requests[1].Method);
        Assert.EndsWith("SKU%20%2B%20%C4%91%E1%BB%8F",handler.Requests[1].Path,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[]{"Quantity"},handler.Requests[1].Body!.Properties().Select(p=>p.Name));
    }
    [Theory] [InlineData(HttpStatusCode.Unauthorized)] [InlineData(HttpStatusCode.Conflict)] [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ApiFailuresStayErrorsInsteadOfBecomingEmptyCart(HttpStatusCode status)
    {
        var handler=new CaptureHandler{Status=status};using var http=new HttpClient(handler);
        var api=new CustomerApiClient(http,"http://backend.test/");
        var error=await Assert.ThrowsAsync<CustomerApiException>(()=>api.CartAsync("token"));
        Assert.Equal((int)status,error.StatusCode);
    }
    [Theory] [InlineData("{}")] [InlineData("{\"items\":null,\"totalQuantity\":0,\"subtotal\":0,\"canCheckout\":false}")]
    public async Task MalformedCartContractCannotBecomeAnEmptyCart(string json)
    {
        var handler=new CaptureHandler{ResponseBody=json};using var http=new HttpClient(handler);
        var api=new CustomerApiClient(http,"http://backend.test/");
        Assert.Equal(502,(await Assert.ThrowsAsync<CustomerApiException>(()=>api.CartAsync("token"))).StatusCode);
    }
}
