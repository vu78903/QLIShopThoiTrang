using QLIShopThoiTrang.Api.Data;
using QLIShopThoiTrang.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalClients", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddSingleton<SqlServerConnectionFactory>();
builder.Services.AddSingleton<SqlServerDatabaseInitializer>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ProductImageStorage>();
builder.Services.AddScoped<StoreRepository>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SqlServerDatabaseInitializer>().InitializeAsync();
}

app.UseCors("AllowLocalClients");
app.UseStaticFiles();

app.MapGet("/", () => Results.Ok(new { app = "QLI Shop Thoi Trang API", database = "Supabase PostgreSQL", status = "running" }));

app.MapGet("/api/health", async (StoreRepository store) =>
{
    try
    {
        return Results.Ok(new { ok = true, database = await store.GetDatabaseNameAsync() });
    }
    catch (Exception ex)
    {
        return Results.Problem("Không kết nối được Supabase PostgreSQL: " + ex.Message);
    }
});

app.MapGet("/api/app-state", async (StoreRepository store) =>
{
    return Results.Ok(await store.GetAppStateAsync());
});

app.MapGet("/api/loai-san-pham", async (StoreRepository store) =>
{
    return Results.Ok(await store.GetProductTypesAsync());
});

app.MapPost("/api/dang-nhap", async (LoginRequestDto input, StoreRepository store) =>
{
    var result = await store.LoginAsync(input);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

app.MapPost("/api/san-pham", async (ProductUpsertDto input, StoreRepository store, ProductImageStorage images) =>
{
    if (string.IsNullOrWhiteSpace(input.Sku) || string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Type))
        return Results.BadRequest("SKU, ten san pham va loai san pham la bat buoc.");
    if (input.Price <= 0)
        return Results.BadRequest("Gia ban phai lon hon 0.");
    if (input.Stock < 0)
        return Results.BadRequest("Ton kho khong duoc am.");

    try
    {
        var oldImage = await store.GetProductImageAsync(input.Sku.Trim().ToUpperInvariant());
        var savedImage = await images.SaveAsync(input.Sku, input.Image);
        var savedProduct = input with { Image = savedImage };
        await store.SaveProductAsync(savedProduct);
        if (!string.Equals(oldImage, savedImage, StringComparison.OrdinalIgnoreCase))
            await images.DeleteManagedFileAsync(oldImage);
        return Results.Ok(savedProduct);
    }
    catch (Exception ex)
    {
        return Results.Problem("Khong luu duoc san pham: " + ex.Message);
    }
});

app.MapPost("/api/san-pham/import", async (List<ProductUpsertDto> input, StoreRepository store) =>
{
    if (input.Count == 0) return Results.BadRequest("File không có sản phẩm để nhập.");
    if (input.Count > 1000) return Results.BadRequest("Mỗi lần chỉ được nhập tối đa 1000 sản phẩm.");
    try
    {
        await store.ImportProductsAsync(input);
        return Results.Ok(new { count = input.Count });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem("Không nhập được danh sách sản phẩm: " + ex.Message);
    }
});

app.MapDelete("/api/san-pham/{sku}", async (string sku, StoreRepository store, ProductImageStorage images) =>
{
    try
    {
        var image = await store.GetProductImageAsync(sku);
        if (!await store.DeleteProductAsync(sku)) return Results.NotFound();
        await images.DeleteManagedFileAsync(image);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapPost("/api/loai-san-pham", async (ProductTypeUpsertDto input, StoreRepository store) =>
{
    if (string.IsNullOrWhiteSpace(input.Id) || string.IsNullOrWhiteSpace(input.Name))
        return Results.BadRequest("Ma loai va ten loai la bat buoc.");

    await store.SaveProductTypeAsync(input);
    return Results.Ok(input);
});

app.MapDelete("/api/loai-san-pham/{maLoai}", async (string maLoai, StoreRepository store) =>
{
    return await store.DeactivateProductTypeAsync(maLoai)
        ? Results.NoContent()
        : Results.NotFound();
});

app.MapPost("/api/doi-tac", async (PartnerUpsertDto input, StoreRepository store) =>
{
    if (string.IsNullOrWhiteSpace(input.Category) || string.IsNullOrWhiteSpace(input.Id) ||
        string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Phone))
        return Results.BadRequest("Loai, ma, ten va so dien thoai la bat buoc.");
    if (!System.Text.RegularExpressions.Regex.IsMatch(input.Phone.Trim(), @"^0\d{9,10}$"))
        return Results.BadRequest("Số điện thoại phải bắt đầu bằng 0 và gồm 10 đến 11 chữ số.");
    if (string.IsNullOrWhiteSpace(input.Email) ||
        !System.Text.RegularExpressions.Regex.IsMatch(input.Email.Trim(), @"^[^@\s]+@[^@\s]+\.com$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        return Results.BadRequest("Email phải đúng định dạng và kết thúc bằng .com.");

    try
    {
        await store.SavePartnerAsync(input);
        return Results.Ok(input);
    }
    catch (Exception ex)
    {
        return Results.Problem("Khong luu duoc doi tac: " + ex.Message);
    }
});

app.MapDelete("/api/doi-tac/{category}/{id}", async (string category, string id, StoreRepository store) =>
{
    return await store.DeactivatePartnerAsync(Uri.UnescapeDataString(category), id)
        ? Results.NoContent()
        : Results.NotFound();
});

app.MapPost("/api/khuyen-mai", async (PromotionUpsertDto input, StoreRepository store) =>
{
    if (string.IsNullOrWhiteSpace(input.Code) || input.Percent <= 0 || input.MinOrder < 0)
        return Results.BadRequest("Ma khuyen mai, phan tram giam va dieu kien la bat buoc.");

    try
    {
        await store.SavePromotionAsync(input);
        return Results.Ok(input);
    }
    catch (Exception ex)
    {
        return Results.Problem("Khong luu duoc khuyen mai: " + ex.Message);
    }
});

app.MapDelete("/api/khuyen-mai/{code}", async (string code, StoreRepository store) =>
{
    return await store.DeactivatePromotionAsync(code)
        ? Results.NoContent()
        : Results.NotFound();
});

app.MapPost("/api/phieu-nhap-kho", async (ImportRequestCreateDto input, StoreRepository store) =>
{
    if (string.IsNullOrWhiteSpace(input.SupplierId) || string.IsNullOrWhiteSpace(input.Sku) || input.Qty <= 0)
        return Results.BadRequest("Nha cung cap, SKU va so luong la bat buoc.");

    try
    {
        return Results.Ok(new { id = await store.CreateImportRequestAsync(input) });
    }
    catch (Exception ex)
    {
        return Results.Problem("Khong tao duoc phieu nhap kho: " + ex.Message);
    }
});

app.MapPost("/api/phieu-nhap-kho/{soPhieu}/duyet", async (string soPhieu, StoreRepository store) =>
{
    try
    {
        await store.ApproveImportRequestAsync(soPhieu);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem("Khong duyet duoc phieu nhap kho: " + ex.Message);
    }
});

app.MapPost("/api/phieu-nhap-kho/{soPhieu}/tu-choi", async (string soPhieu, StoreRepository store) =>
{
    return await store.RejectImportRequestAsync(soPhieu) ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/api/hoa-don", async (InvoiceCreateDto input, StoreRepository store) =>
{
    if (string.IsNullOrWhiteSpace(input.Id) || input.Items.Count == 0 || input.Total < 0)
        return Results.BadRequest("Hóa đơn và danh sách sản phẩm không hợp lệ.");
    var deliveryMode = (input.DeliveryMode ?? string.Empty).Trim().ToUpperInvariant();
    var isCod = input.IsCod || deliveryMode == "COD";
    var isDelivery = isCod || deliveryMode == "DELIVERY";
    if (input.ShippingFee < 0)
        return Results.BadRequest("Phí giao hàng không được âm.");
    if (isDelivery && (string.IsNullOrWhiteSpace(input.ReceiverName)
        || string.IsNullOrWhiteSpace(input.ReceiverPhone)
        || string.IsNullOrWhiteSpace(input.ReceiverAddress)))
        return Results.BadRequest("Đơn giao hàng phải có tên, số điện thoại và địa chỉ người nhận.");
    if (isDelivery && !System.Text.RegularExpressions.Regex.IsMatch(input.ReceiverPhone!, @"^0\d{9,10}$"))
        return Results.BadRequest("Số điện thoại người nhận không hợp lệ.");
    var taxableAmount = Math.Max(0, input.Subtotal - input.Discount);
    var expectedVat = Math.Round(taxableAmount * 0.08m, 0, MidpointRounding.AwayFromZero);
    var expectedTotal = taxableAmount + expectedVat + (isDelivery ? input.ShippingFee : 0);
    if (Math.Abs(input.VatAmount - expectedVat) > 1 || Math.Abs(input.Total - expectedTotal) > 1)
        return Results.BadRequest("Số tiền VAT hoặc tổng thanh toán không hợp lệ.");
    try
    {
        await store.CreateInvoiceAsync(input);
        return Results.Ok(new { id = input.Id });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem("Không lưu được hóa đơn: " + ex.Message);
    }
});

app.MapGet("/api/hoa-don", async (string? q, StoreRepository store) =>
{
    return Results.Ok(await store.GetInvoicesAsync(q));
});

app.MapGet("/api/hoa-don/{soHoaDon}", async (string soHoaDon, StoreRepository store) =>
{
    var invoice = await store.GetInvoiceAsync(soHoaDon);
    return invoice is null ? Results.NotFound() : Results.Ok(invoice);
});

app.MapPut("/api/hoa-don/{soHoaDon}/giao-hang", async (string soHoaDon, CodStatusUpdateDto input, StoreRepository store) =>
{
    try
    {
        await store.UpdateDeliveryStatusAsync(soHoaDon, input);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem("Không cập nhật được trạng thái giao hàng: " + ex.Message);
    }
});

app.MapDelete("/api/hoa-don/{soHoaDon}/cod", async (string soHoaDon, StoreRepository store) =>
{
    return await store.DeletePendingCodAsync(soHoaDon) ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/api/doi-tra/tra-cuu", async (string? q, StoreRepository store) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<ReturnInvoiceSummaryDto>());
    return Results.Ok(await store.SearchReturnInvoicesAsync(q));
});

app.MapGet("/api/doi-tra/hoa-don/{soHoaDon}", async (string soHoaDon, StoreRepository store) =>
{
    var invoice = await store.GetReturnInvoiceAsync(soHoaDon);
    return invoice is null ? Results.NotFound() : Results.Ok(invoice);
});

app.MapGet("/api/doi-tra", async (StoreRepository store) =>
{
    return Results.Ok(await store.GetReturnsAsync());
});

app.MapGet("/api/doi-tra/{soPhieu}", async (string soPhieu, StoreRepository store) =>
{
    var receipt = await store.GetReturnAsync(soPhieu);
    return receipt is null ? Results.NotFound() : Results.Ok(receipt);
});

app.MapPost("/api/doi-tra", async (ReturnCreateDto input, StoreRepository store) =>
{
    if (string.IsNullOrWhiteSpace(input.InvoiceId) || string.IsNullOrWhiteSpace(input.ReturnSku) ||
        input.Qty <= 0 || string.IsNullOrWhiteSpace(input.Reason) || string.IsNullOrWhiteSpace(input.Resolution))
        return Results.BadRequest("Hóa đơn, sản phẩm, số lượng, lỗi và hình thức xử lý là bắt buộc.");

    try
    {
        return Results.Ok(await store.CreateReturnAsync(input));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem("Không tạo được phiếu đổi trả: " + ex.Message);
    }
});

app.Run();
