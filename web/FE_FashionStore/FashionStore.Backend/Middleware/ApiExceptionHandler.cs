using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace FashionStore.Backend.Middleware;

public sealed class CatalogConfigurationException(string message) : Exception(message);
public sealed class CustomerRequestException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken cancellationToken)
    {
        if (error is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return false;
        var sqlError = error as SqlException ?? (error as Microsoft.EntityFrameworkCore.DbUpdateException)?.InnerException as SqlException;
        var featureError = sqlError?.Number is >= 55201 and <= 55220;
        var inner = error;
        Npgsql.NpgsqlException? pgError = null;
        while (inner != null) { if (inner is Npgsql.NpgsqlException pg) { pgError = pg; break; } inner = inner.InnerException; }
        var conflict = error is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException || sqlError?.Number is 2601 or 2627 or 1205 ||
            pgError is Npgsql.PostgresException { SqlState: "23505" or "40001" or "40P01" };
        var unavailable = sqlError != null || pgError != null || error is CatalogConfigurationException;
        var status = error is CustomerRequestException requestError ? requestError.Status : (conflict || featureError) ? 409 : unavailable ? 503 : 500;
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        if (error is CustomerRequestException || conflict)
            logger.LogWarning("Customer request rejected with status {Status}; trace {TraceId}", status, traceId);
        else logger.LogError(error, "API request failed with status {Status}; trace {TraceId}", status, traceId);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = featureError ? sqlError!.Message : error is CustomerRequestException business ? business.Message : conflict
                ? "Dữ liệu đã thay đổi hoặc thông tin đã được sử dụng. Vui lòng tải lại và kiểm tra."
                : unavailable ? "Dịch vụ chưa sẵn sàng. Vui lòng thử lại sau." : "Chưa thể xử lý yêu cầu. Vui lòng thử lại sau.",
            Type = "https://httpstatuses.com/" + status,
            Extensions = { ["traceId"] = traceId }
        }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
