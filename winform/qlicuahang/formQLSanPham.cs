using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang 
{
    public partial class formQLSanPham : Form 
    {
        // Form riêng để mở nhanh màn hình quản lý sản phẩm.
        private WebView2 webView = null!;

        public formQLSanPham()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(1300, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Load += formQLSanPham_Load;
        }

        private async void formQLSanPham_Load(object? sender, EventArgs e)
        {
            // Load trang sản phẩm.
            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);

            await webView.EnsureCoreWebView2Async(null);

            string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "san-pham.html");
            if (File.Exists(fileHtml))
            {
                webView.CoreWebView2.Navigate(fileHtml);

                webView.NavigationCompleted += (s, args) =>
                {
                    // Vào thẳng danh sách sản phẩm.
                    webView.CoreWebView2.ExecuteScriptAsync("showView('products');");
                };
            }
        }
    }
}
