using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang;

public partial class formGiaoHang : Form
{
    private WebView2 webView = null!;

    public formGiaoHang()
    {
        InitializeComponent();
        WindowState = FormWindowState.Normal;
        Size = new Size(1300, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Load += FormGiaoHang_Load;
    }

    private async void FormGiaoHang_Load(object? sender, EventArgs e)
    {
        webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(webView);
        await webView.EnsureCoreWebView2Async(null);

        string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "giao-hang.html");
        if (File.Exists(fileHtml)) webView.CoreWebView2.Navigate(fileHtml);
    }
}
