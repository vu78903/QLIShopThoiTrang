using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace qlicuahang;

internal static class ApiProcessManager
{
    private const string HealthUrl = "http://127.0.0.1:5000/api/health";
    private const int ApiPort = 5000;
    private static Process? ownedProcess;
    private static readonly HttpClient HealthClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public static async Task<bool> EnsureRunningAsync()
    {
        string apiDirectory = Path.Combine(AppContext.BaseDirectory, "api");
        string executablePath = Path.Combine(apiDirectory, "QLIShopThoiTrang.Api.exe");
        string dllPath = Path.Combine(apiDirectory, "QLIShopThoiTrang.Api.dll");

        if (await WaitForHealthyAsync(2, 250))
        {
            ownedProcess = FindBundledApiProcess(executablePath);
            return true;
        }

        // Chỉ dừng tiến trình API cũ nếu nó chạy đúng từ thư mục output của WinForms.
        if (await IsPortOpenAsync())
        {
            Process? staleProcess = FindBundledApiProcess(executablePath);
            if (staleProcess is null) return false;
            try
            {
                staleProcess.Kill(entireProcessTree: true);
                await staleProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                return false;
            }
        }

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
        CopyUserEnvironment(startInfo, "PAYOS_CLIENT_ID");
        CopyUserEnvironment(startInfo, "PAYOS_API_KEY");
        CopyUserEnvironment(startInfo, "PAYOS_CHECKSUM_KEY");
        CopyUserEnvironment(startInfo, "PAYOS_RETURN_URL");
        CopyUserEnvironment(startInfo, "PAYOS_CANCEL_URL");

        try
        {
            ownedProcess = Process.Start(startInfo);
            if (ownedProcess is null) return false;
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
            if (!response.IsSuccessStatusCode) return false;
            await using Stream content = await response.Content.ReadAsStreamAsync();
            using JsonDocument json = await JsonDocument.ParseAsync(content);
            return json.RootElement.TryGetProperty("ok", out JsonElement ok) && ok.GetBoolean()
                && json.RootElement.TryGetProperty("qrPayment", out JsonElement qrPayment)
                && qrPayment.GetBoolean();
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

    public static void StopOwnedProcess()
    {
        Process? process = ownedProcess;
        ownedProcess = null;
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ứng dụng vẫn đóng bình thường nếu API đã tự kết thúc.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static Process? FindBundledApiProcess(string executablePath)
    {
        string expectedPath = Path.GetFullPath(executablePath);
        foreach (Process process in Process.GetProcessesByName("QLIShopThoiTrang.Api"))
        {
            try
            {
                string? actualPath = process.MainModule?.FileName;
                if (actualPath is not null && string.Equals(
                        Path.GetFullPath(actualPath), expectedPath, StringComparison.OrdinalIgnoreCase))
                    return process;
                process.Dispose();
            }
            catch
            {
                process.Dispose();
            }
        }
        return null;
    }

    private static void CopyUserEnvironment(ProcessStartInfo startInfo, string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(value)) startInfo.Environment[variableName] = value;
    }
}
