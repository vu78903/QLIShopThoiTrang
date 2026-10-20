using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace qlicuahang
{
    public partial class formTongQuan : Form
    {
        // Màn hình chính dùng WebView2 để chạy giao diện POS viết bằng HTML/CSS/JS.
        private WebView2? webView;

        public formTongQuan()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(2500, 1460);
            this.StartPosition = FormStartPosition.CenterScreen;

            this.Load += FormTongQuan_Load;
        }

        private async void FormTongQuan_Load(object? sender, EventArgs e)
        {
            if (webView == null)
            {
                // Chỉ tạo WebView một lần, tránh load lại giao diện khi quay lại từ đăng nhập.
                webView = new WebView2 { Dock = DockStyle.Fill };
                this.Controls.Add(webView);
                await webView.EnsureCoreWebView2Async(null);

                webView.CoreWebView2.WebMessageReceived += (s, args) =>
                {
                    // JavaScript gửi "logout" lên đây khi người dùng bấm đăng xuất.
                    string message = args.TryGetWebMessageAsString();
                    if (message == "logout")
                    {
                        this.Hide();

                        formDangNhap login = new formDangNhap();
                        if (login.ShowDialog() == DialogResult.OK)
                        {
                            CapNhatGiaoDienTheoQuyen();
                            this.Show();
                        }
                        else
                        {
                            Application.Exit();
                        }
                    }
                };

                string fileHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bao-cao.html");
                if (File.Exists(fileHtml))
                {
                    webView.CoreWebView2.Navigate(fileHtml);
                    webView.NavigationCompleted += (s, args) =>
                    {
                        if (args.IsSuccess) CapNhatGiaoDienTheoQuyen();
                    };
                }
            }
            else
            {
                CapNhatGiaoDienTheoQuyen();
            }
        }

        // Cập nhật giao diện theo vai trò đang đăng nhập.
        private async void CapNhatGiaoDienTheoQuyen()
        {
            if (webView == null || webView.CoreWebView2 == null) return;

            string roleKey;
            string roleLabel;
            string roleTitle;
            string defaultView;
            string allowedViews;
            string deniedMessage;

            switch (formDangNhap.VaiTro)
            {
                case "CuaHangTruong":
                    roleKey = "storeManager";
                    roleLabel = "Vai trò: Cửa hàng trưởng";
                    roleTitle = "Cửa hàng trưởng";
                    defaultView = "report";
                    allowedViews = "'report','pos','delivery','returns','partners','products','product-types','inventory','promotions'";
                    deniedMessage = "Cửa hàng trưởng có toàn quyền trong hệ thống";
                    break;
                case "TruongCa":
                    roleKey = "shiftLeader";
                    roleLabel = "Vai trò: Trưởng ca";
                    roleTitle = "Trưởng ca";
                    defaultView = "pos";
                    allowedViews = "'report','pos','delivery','returns','inventory'";
                    deniedMessage = "Trưởng ca chỉ quản lý hoạt động trong ca, không được thay đổi dữ liệu quản trị";
                    break;
                case "NhanVienKho":
                    roleKey = "warehouse";
                    roleLabel = "Vai trò: Nhân viên kho";
                    roleTitle = "Nhân viên kho";
                    defaultView = "inventory";
                    allowedViews = "'inventory'";
                    deniedMessage = "Nhân viên kho chỉ có quyền Tồn kho / Nhập kho";
                    break;
                default:
                    roleKey = "cashier";
                    roleLabel = "Vai trò: Thu ngân";
                    roleTitle = "Nhân viên thu ngân";
                    defaultView = "pos";
                    allowedViews = "'pos','delivery','returns'";
                    deniedMessage = "Thu ngân chỉ có quyền mở POS bán hàng, Giao hàng và Đổi trả";
                    break;
            }

            string userDisplay = string.IsNullOrEmpty(formDangNhap.TenHienThi) ? "Người dùng" : formDangNhap.TenHienThi;
            string employeeIdJson = JsonSerializer.Serialize(formDangNhap.MaNhanVien ?? "");
            string employeeNameJson = JsonSerializer.Serialize(userDisplay);

            string jsCode = $@"
                (function() {{
                    window.userRole = '{roleKey}';
                    window.currentEmployee = {{ id: {employeeIdJson}, name: {employeeNameJson} }};
                    window.allowedViews = [{allowedViews}];
                    window.roleDeniedMessage = '{deniedMessage}';

                    if (!document.getElementById('role-permission-style')) {{
                        const style = document.createElement('style');
                        style.id = 'role-permission-style';
                        style.innerHTML = '.nav-btn * {{ pointer-events: none !important; }}';
                        document.head.appendChild(style);
                    }}

                    const roleSwitch = document.getElementById('roleSwitch');
                    if (roleSwitch) {{
                        roleSwitch.innerHTML = '<i data-lucide=""shield-check"" style=""width:15px""></i> {roleLabel}';
                    }}

                    const roleText = document.getElementById('roleText');
                    if (roleText) {{
                        roleText.innerText = '{roleTitle}';
                    }}

                    const userName = document.querySelector('.user-info strong');
                    if (userName) {{
                        userName.innerText = '{userDisplay}';
                    }}

                    const allowedSet = new Set(window.allowedViews || []);

                    document.querySelectorAll('.nav-btn[data-view]').forEach(btn => {{
                        const view = btn.getAttribute('data-view');
                        const allowed = allowedSet.has(view);
                        btn.classList.toggle('hidden', !allowed);
                        btn.disabled = !allowed;
                        btn.onclick = function() {{
                            const target = this.getAttribute('data-view');
                            if (!target) return;

                            if (!allowedSet.has(target)) {{
                                if (typeof toast === 'function') toast(window.roleDeniedMessage || 'Bạn không có quyền mở chức năng này');
                                return;
                            }}

                            if (typeof showView === 'function') {{
                                showView(target);
                            }}
                        }};
                    }});

                    document.querySelectorAll('.nav-btn.manager-only:not([data-view])').forEach(btn => {{
                        const canUseSystemSetting = '{roleKey}' === 'storeManager';
                        btn.classList.toggle('hidden', !canUseSystemSetting);
                        btn.disabled = !canUseSystemSetting;
                    }});

                    const createImportButton = document.querySelector('button[onclick=""openAddImportRequest()""]');
                    if (createImportButton) {{
                        const canCreateImport = ['storeManager', 'warehouse'].includes('{roleKey}');
                        createImportButton.classList.toggle('hidden', !canCreateImport);
                        createImportButton.disabled = !canCreateImport;
                    }}

                    if (window.lucide && typeof lucide.createIcons === 'function') {{
                        lucide.createIcons();
                    }}

                    if (typeof showView === 'function') {{
                        const currentView = document.body.getAttribute('data-view') || '{defaultView}';
                        showView(allowedSet.has(currentView) ? currentView : '{defaultView}');
                    }}
                }})();
            ";

            await webView.CoreWebView2.ExecuteScriptAsync(jsCode);
        }
    } 
} 
