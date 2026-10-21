using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang
{
    public partial class formBanHang : Form
    {
        // WebView2 dùng để nhúng giao diện HTML vào trong WinForms.
        private WebView2 webView = null!;

        public formBanHang()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(2500, 1460);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Load += FormBanHang_Load;
        }

        private async void FormBanHang_Load(object? sender, EventArgs e)
        {
            // Tạo trình duyệt nội bộ rồi mở thẳng trang bán hàng.
            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);

            await webView.EnsureCoreWebView2Async(null);

            string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "ban-hang.html");
            if (File.Exists(fileHtml))
            {
                webView.CoreWebView2.Navigate(fileHtml);

                // Sau khi web load xong, tự chuyển sang màn hình bán hàng.
                webView.NavigationCompleted += (s, args) =>
                {
                    webView.CoreWebView2.ExecuteScriptAsync("showView('pos');");
                };
            }
        }
    }
}
