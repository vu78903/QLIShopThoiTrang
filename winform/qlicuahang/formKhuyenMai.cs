using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang 
{
    public partial class formKhuyenMai : Form 
    {
        // Form riêng để mở nhanh màn hình mã khuyến mãi.
        private WebView2 webView = null!;

        public formKhuyenMai()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(1300, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Load += formKhuyenMai_Load;
        }

        private async void formKhuyenMai_Load(object? sender, EventArgs e)
        {
            // Load giao diện web rồi chuyển sang phần voucher/khuyến mãi.
            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);

            await webView.EnsureCoreWebView2Async(null);

            string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "khuyen-mai.html");
            if (File.Exists(fileHtml))
            {
                webView.CoreWebView2.Navigate(fileHtml);

                webView.NavigationCompleted += (s, args) =>
                {
                    // Vào thẳng màn hình khuyến mãi.
                    webView.CoreWebView2.ExecuteScriptAsync("showView('promotions');");
                };
            }
        }
    }
}
