using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace QLIShopThoiTrang.Api.Services;

// Chống gửi lặp khi nút bị bấm nhanh hoặc client gửi lại vì mất phản hồi.
// Chứng từ có tồn kho vẫn dùng transaction/idempotency trong database.
public sealed class MutationRequests
{
    private sealed class Attempt
    {
        public readonly SemaphoreSlim Gate=new(1,1);
        public string? Digest;
        public byte[]? Body;
        public int Status;
        public string? ContentType;
        public DateTimeOffset LastUsed=DateTimeOffset.UtcNow;
    }
    private readonly ConcurrentDictionary<string,Attempt> attempts=new();

    public async Task InvokeAsync(HttpContext context,RequestDelegate next)
    {
        var request=context.Request;
        var key=request.Headers["X-Request-ID"].ToString();
        if(request.Method is "GET" or "OPTIONS" || string.IsNullOrEmpty(key) || context.Items["StaffUser"] is not Models.LoginResultDto user)
        {await next(context);return;}
        if(key.Length>80){context.Response.StatusCode=400;return;}
        request.EnableBuffering();
        using var hash=SHA256.Create();var bodyHash=await hash.ComputeHashAsync(request.Body);request.Body.Position=0;
        var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Method+request.Path+request.QueryString+Convert.ToHexString(bodyHash))));
        if(attempts.Count>2000)
            foreach(var pair in attempts.Where(x=>x.Value.LastUsed<DateTimeOffset.UtcNow.AddMinutes(-30) && x.Value.Gate.CurrentCount==1))attempts.TryRemove(pair.Key,out _);
        var attempt=attempts.GetOrAdd(user.EmployeeId+":"+key,_=>new());
        await attempt.Gate.WaitAsync(context.RequestAborted);
        try
        {
            attempt.LastUsed=DateTimeOffset.UtcNow;
            if(attempt.Digest is not null && attempt.Digest!=digest)
            {context.Response.StatusCode=409;await context.Response.WriteAsJsonAsync(new{message="Mã yêu cầu đã dùng cho nội dung khác."});return;}
            attempt.Digest=digest;
            if(attempt.Body is not null)
            {
                context.Response.StatusCode=attempt.Status;context.Response.ContentType=attempt.ContentType;
                if(attempt.Body.Length>0)await context.Response.Body.WriteAsync(attempt.Body,context.RequestAborted);
                return;
            }
            var original=context.Response.Body;await using var buffer=new MemoryStream();context.Response.Body=buffer;
            try
            {
                await next(context);
                var response=buffer.ToArray();
                if(context.Response.StatusCode is >=200 and <300)
                {attempt.Body=response;attempt.Status=context.Response.StatusCode;attempt.ContentType=context.Response.ContentType;}
                context.Response.Body=original;
                await original.WriteAsync(response,context.RequestAborted);
            }
            finally{context.Response.Body=original;}
        }
        finally{attempt.Gate.Release();}
    }
}
