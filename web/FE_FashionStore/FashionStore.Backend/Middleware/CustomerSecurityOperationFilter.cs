using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FashionStore.Backend.Middleware;

public sealed class CustomerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attributes = context.MethodInfo.GetCustomAttributes(true)
            .Concat(context.MethodInfo.DeclaringType!.GetCustomAttributes(true));
        if (!attributes.OfType<AuthorizeAttribute>().Any()) return;
        operation.Security = new List<OpenApiSecurityRequirement> { new() {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() } };
    }
}
