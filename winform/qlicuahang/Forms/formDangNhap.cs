using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang;

public partial class formDangNhap : Form
{
    public static string VaiTro = "";
    public static string TenHienThi = "";
    public static string MaNhanVien = "";

    private readonly WebView2 webView = new() { Dock = DockStyle.Fill };
    private bool dangXuLy;

    public formDangNhap()
    {
        InitializeComponent();
        Text = "Đăng nhập - Hệ thống quản lý cửa hàng Yody";
        ClientSize = new Size(1080, 680);
        MinimumSize = new Size(920, 600);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Normal;
        BackColor = Color.FromArgb(225, 231, 238);
        Controls.Add(webView);
        Load += FormDangNhap_Load;
        Shown += (_, _) =>
        {
            WindowState = FormWindowState.Normal;
            ClientSize = new Size(1080, 680);
            CenterToScreen();
        };
    }

    // WebView2 hiển thị giao diện HTML, còn C# chịu trách nhiệm gọi API đăng nhập.
    private async void FormDangNhap_Load(object? sender, EventArgs e)
    {
        try
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

            string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "dang-nhap.html");
            if (!File.Exists(fileHtml))
            {
                MessageBox.Show("Không tìm thấy Views/dang-nhap.html.", "Lỗi giao diện",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            webView.CoreWebView2.Navigate(fileHtml);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không khởi tạo được giao diện đăng nhập: " + ex.Message,
                "Lỗi WebView2", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private async void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        LoginMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<LoginMessage>(e.WebMessageAsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return;
        }

        if (message?.Type != "login" || dangXuLy) return;
        await DangNhapAsync(message.Username?.Trim() ?? "", message.Password ?? "");
    }

    private async Task DangNhapAsync(string taiKhoan, string matKhau)
    {
        if (string.IsNullOrWhiteSpace(taiKhoan) || string.IsNullOrWhiteSpace(matKhau))
        {
            await BaoKetQuaAsync(false, "Vui lòng nhập đầy đủ tên tài khoản và mật khẩu.");
            return;
        }

        dangXuLy = true;
        await DatTrangThaiDangXuLyAsync(true);

        try
        {
            using HttpClient client = new() { BaseAddress = new Uri("http://127.0.0.1:5000") };
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/dang-nhap",
                new { username = taiKhoan, password = matKhau });

            if (!response.IsSuccessStatusCode)
            {
                await BaoKetQuaAsync(false, "Tài khoản hoặc mật khẩu không chính xác.");
                return;
            }

            LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (login is null) throw new InvalidOperationException("API không trả về thông tin nhân viên.");

            VaiTro = login.Role;
            TenHienThi = login.DisplayName;
            MaNhanVien = login.EmployeeId;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (HttpRequestException)
        {
            await BaoKetQuaAsync(false, "Không kết nối được hệ thống. Vui lòng kiểm tra API và thử lại.");
        }
        catch (Exception ex)
        {
            await BaoKetQuaAsync(false, "Không thể đăng nhập: " + ex.Message);
        }
        finally
        {
            dangXuLy = false;
            if (!IsDisposed) await DatTrangThaiDangXuLyAsync(false);
        }
    }

    private Task BaoKetQuaAsync(bool thanhCong, string noiDung)
    {
        string text = JsonSerializer.Serialize(noiDung);
        return webView.ExecuteScriptAsync($"window.completeLogin({thanhCong.ToString().ToLowerInvariant()}, {text});");
    }

    private Task DatTrangThaiDangXuLyAsync(bool dangTai) =>
        webView.ExecuteScriptAsync($"window.setLoginBusy({dangTai.ToString().ToLowerInvariant()});");

    private sealed record LoginMessage(string? Type, string? Username, string? Password);
    private sealed record LoginResponse(string EmployeeId, string DisplayName, string Role);
}
