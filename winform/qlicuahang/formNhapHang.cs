using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang 
{
    public partial class formNhapHang : Form 
    {
        // Tạm thời dùng màn hình tồn kho để phục vụ phần nhập hàng/quản lý kho.
        private WebView2 webView = null!;

        public formNhapHang()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(1300, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Load += formNhapHang_Load;
        }

        private async void formNhapHang_Load(object? sender, EventArgs e)
        {
            // Load giao diện web rồi chuyển sang view kho.
            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);

            await webView.EnsureCoreWebView2Async(null);

            string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ton-kho.html");
            if (File.Exists(fileHtml))
            {
                webView.CoreWebView2.Navigate(fileHtml);

                webView.NavigationCompleted += (s, args) =>
                {
                    // Hiện tại nhập hàng đang dùng chung màn hình tồn kho.
                    webView.CoreWebView2.ExecuteScriptAsync("showView('inventory');");
                };
            }
        }
    }
}
