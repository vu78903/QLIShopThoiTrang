using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace qlicuahang;

internal static class ApiProcessManager
{
    private const string HealthUrl = "http://127.0.0.1:5000/api/health";
    private const int ApiPort = 5000;
    private static readonly HttpClient HealthClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public static async Task<bool> EnsureRunningAsync()
    {
        if (await WaitForHealthyAsync(2, 250)) return true;

        // Cổng đã có tiến trình giữ thì chờ API hiện tại sẵn sàng, không mở thêm bản thứ hai.
        if (await IsPortOpenAsync())
            return await WaitForHealthyAsync(6, 500);

        string apiDirectory = Path.Combine(AppContext.BaseDirectory, "api");
        string executablePath = Path.Combine(apiDirectory, "QLIShopThoiTrang.Api.exe");
        string dllPath = Path.Combine(apiDirectory, "QLIShopThoiTrang.Api.dll");

        if (!File.Exists(executablePath) && !File.Exists(dllPath))
            return false;

        ProcessStartInfo startInfo;
        if (File.Exists(executablePath))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--urls http://127.0.0.1:5000"
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\" --urls http://127.0.0.1:5000"
            };
        }

        startInfo.WorkingDirectory = apiDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;

        string? connectionString = Environment.GetEnvironmentVariable(
            "YODYSTORE_CONNECTION_STRING", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;
        startInfo.Environment["YODYSTORE_CONNECTION_STRING"] = connectionString;

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null) return false;
        }
        catch
        {
            return false;
        }

        return await WaitForHealthyAsync(30, 500);
    }

    private static async Task<bool> WaitForHealthyAsync(int attempts, int delayMilliseconds)
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (await IsHealthyAsync()) return true;
            if (attempt + 1 < attempts)
                await Task.Delay(delayMilliseconds);
        }

        return false;
    }

    private static async Task<bool> IsHealthyAsync()
    {
        try
        {
            using HttpResponseMessage response = await HealthClient.GetAsync(HealthUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsPortOpenAsync()
    {
        try
        {
            using TcpClient client = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(1));
            await client.ConnectAsync(IPAddress.Loopback, ApiPort, timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
