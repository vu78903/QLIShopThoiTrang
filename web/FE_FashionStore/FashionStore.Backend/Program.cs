using FashionStore.Backend.Data;
using FashionStore.Backend.Middleware;
using FashionStore.Backend.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using FashionStore.Backend.Models;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "FashionStore API", Version = "v1",
    Description = "Catalog, customer/cart, administrator dashboard and DEV checkout/order management."
}));
builder.Services.ConfigureSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http,
        Scheme = "bearer", Description = "Use accessToken from POST /api/account/login. DEV session expires after one hour." });
    options.OperationFilter<CustomerSecurityOperationFilter>();
});
builder.Services.AddSingleton<DatabaseMode>();
builder.Services.AddDbContext<SupabaseCatalogDbContext>((services, options) =>
{
    options.UseNpgsql(services.GetRequiredService<DatabaseMode>().SupabaseConnection,
        postgres => postgres.CommandTimeout(15));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
builder.Services.AddDbContext<FashionStoreDbContext>((services, options) =>
{
    if (services.GetRequiredService<DatabaseMode>().IsSupabase)
    {
        var mode = services.GetRequiredService<DatabaseMode>();
        if (!mode.CustomerFeaturesEnabled) throw new CatalogConfigurationException("Supabase customer migration is not enabled.");
        options.UseNpgsql(mode.SupabaseConnection, postgres => postgres.CommandTimeout(15));
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        return;
    }
    var connection = builder.Configuration.GetConnectionString("FashionStore");
    if (string.IsNullOrWhiteSpace(connection))
        throw new CatalogConfigurationException("Configure ConnectionStrings:FashionStore before querying the catalog.");
    var settings = new SqlConnectionStringBuilder(connection);
    if (builder.Environment.IsDevelopment() && settings.InitialCatalog != "FashionStoreDB_Dev")
        throw new CatalogConfigurationException("Development catalog must use the restored FashionStoreDB_Dev copy.");
    options.UseSqlServer(connection, sql => sql.CommandTimeout(15));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<SupabaseCatalogService>();
builder.Services.AddScoped<ICatalogService>(services => services.GetRequiredService<DatabaseMode>().IsSupabase
    ? services.GetRequiredService<SupabaseCatalogService>()
    : services.GetRequiredService<CatalogService>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CustomerSessions>();
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210000);
builder.Services.AddSingleton<IPasswordHasher<TaiKhoanKhachHang>, PasswordHasher<TaiKhoanKhachHang>>();
builder.Services.AddSingleton<CustomerPasswordVerifier>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<SupabaseOrderService>();
builder.Services.AddScoped<CustomerFeaturesService>();
builder.Services.AddScoped<PasswordRecoveryService>();
builder.Services.Configure<AdminRoleOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.AddSingleton<IPasswordHasher<NhanVien>, PasswordHasher<NhanVien>>();
builder.Services.AddSingleton<AdminSessions>();
builder.Services.AddSingleton<AdminPasswordVerifier>();
builder.Services.AddScoped<AdminAccess>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddAuthentication(CustomerAuthentication.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, CustomerAuthentication>(CustomerAuthentication.SchemeName, _ => { })
    .AddScheme<AuthenticationSchemeOptions, AdminAuthentication>(AdminAuthentication.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "300";
        await Results.Problem(statusCode: 429, title: "Quá nhiều lần thử. Vui lòng thử lại sau.").ExecuteAsync(context.HttpContext);
    };
    options.AddPolicy("admin-login", context => RateLimitPartition.GetFixedWindowLimiter(
        "admin:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"), _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
// Explicitly block unmigrated controllers before authentication or service activation.
app.Use(async (context, next) =>
{
    var controller = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
    var mode = context.RequestServices.GetRequiredService<DatabaseMode>();
    var customerController = controller?.ControllerTypeInfo.AsType() == typeof(FashionStore.Backend.Controllers.AccountController) ||
        controller?.ControllerTypeInfo.AsType() == typeof(FashionStore.Backend.Controllers.CartController);
    if (mode.IsSupabase && controller != null && !(mode.CustomerFeaturesEnabled && customerController) &&
        !(mode.OrdersEnabled && controller.ControllerTypeInfo.AsType() == typeof(FashionStore.Backend.Controllers.OrdersController)) &&
        controller.ControllerTypeInfo.AsType() != typeof(FashionStore.Backend.Controllers.ProductsController) &&
        controller.ControllerTypeInfo.AsType() != typeof(FashionStore.Backend.Controllers.CategoriesController))
    {
        await Results.Problem(statusCode: 503,
            title: "Chức năng này chưa được bật trong giai đoạn chuyển sang Supabase.")
            .ExecuteAsync(context);
        return;
    }
    await next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// MVC calls the API server-to-server. No browser CORS policy is required.
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (IServiceProvider services, DatabaseMode mode, CancellationToken cancellationToken) =>
    await (mode.IsSupabase
        ? services.GetRequiredService<SupabaseCatalogDbContext>().Database
        : services.GetRequiredService<FashionStoreDbContext>().Database).CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "database-connected" })
        : Results.Problem(statusCode: 503, title: "Database connection is unavailable."));
app.Run();

public partial class Program { }
