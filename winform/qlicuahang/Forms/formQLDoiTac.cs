using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang 
{
    public partial class formQLDoiTac : Form 
    {
        // Form riêng để mở nhanh màn hình khách hàng, nhà cung cấp, nhân viên.
        private WebView2 webView = null!;

        public formQLDoiTac()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(1300, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Load += formQLDoiTac_Load;
        }

        private async void formQLDoiTac_Load(object? sender, EventArgs e)
        {
            // Load trang đối tác.
            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);

            await webView.EnsureCoreWebView2Async(null);

            string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "doi-tac.html");
            if (File.Exists(fileHtml))
            {
                webView.CoreWebView2.Navigate(fileHtml);

                webView.NavigationCompleted += (s, args) =>
                {
                    // Vào thẳng màn hình quản lý đối tác.
                    webView.CoreWebView2.ExecuteScriptAsync("showView('partners');");
                };
            }
        }
    }
}
