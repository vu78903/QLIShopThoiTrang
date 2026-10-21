using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang
{
    public partial class formCOD : Form
    {
        // Form này chỉ là vỏ WinForms, nội dung thật nằm trong trang giao hàng COD.
        private WebView2 webView = null!;

        public formCOD()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(1300, 780);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Load += FormCOD_Load;
        }

        private async void FormCOD_Load(object? sender, EventArgs e)
        {
            // Mở giao diện web và đưa người dùng thẳng tới màn hình COD.
            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);

            await webView.EnsureCoreWebView2Async(null);

            string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "giao-hang-cod.html");
            if (File.Exists(fileHtml))
            {
                webView.CoreWebView2.Navigate(fileHtml);

                // Tự chuyển sang màn hình xử lý đơn giao hàng COD.
                webView.NavigationCompleted += (s, args) =>
                {
                    webView.CoreWebView2.ExecuteScriptAsync("showView('cod');");
                };
            }
        }
    }
}
