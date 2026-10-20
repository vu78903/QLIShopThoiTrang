using System;
using System.Drawing;
using System.Net.Http.Json;
using System.Windows.Forms;

namespace qlicuahang
{
    public partial class formDangNhap : Form
    {
        // Hai biến này được form chính đọc để biết ai đang đăng nhập và có quyền gì.
        public static string VaiTro = "";      
        public static string TenHienThi = "";
        public static string MaNhanVien = "";

        private TextBox txtTenDangNhap = null!;
        private TextBox txtMatKhau = null!;
        private CheckBox chkHienMatKhau = null!;
        private Button btnDangNhap = null!;
        private Button btnThoat = null!;

        public formDangNhap()
        {
            InitializeComponent();
            DungGiaoDienYody();
        }

        private void DungGiaoDienYody()
        {
            // Dựng giao diện đăng nhập bằng code để không phụ thuộc nhiều vào Designer.
            this.Text = "Đăng Nhập Hệ Thống - Yody";
            this.Size = new Size(1640, 960);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.Controls.Clear();

            Panel pnlLeft = new Panel { Dock = DockStyle.Left, Width = 380, BackColor = Color.FromArgb(20, 20, 110) };
            Label lblLogo = new Label { Text = "Yody", Font = new Font("Georgia", 32F, FontStyle.Bold), ForeColor = Color.FromArgb(254, 215, 102), AutoSize = true, Location = new Point(45, 55) };
            Label lblVer = new Label { Text = "POS V2026", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.WhiteSmoke, AutoSize = true, Location = new Point(190, 75) };
            Label lblSub = new Label { Text = "QUẢN LÍ BÁN HÀNG", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.LightSteelBlue, AutoSize = true, Location = new Point(60, 150) };
            Label lblBranchTitle = new Label { Text = "Chi nhánh Tân Sơn Nhì", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(48, 330) };
            Label lblBranchAddr = new Label { Text = "304 Tân Sơn Nhì, Q. Tân Phú", Font = new Font("Segoe UI", 9F), ForeColor = Color.LightGray, AutoSize = true, Location = new Point(48, 355) };

            pnlLeft.Controls.AddRange(new Control[] { lblLogo, lblVer, lblSub, lblBranchTitle, lblBranchAddr });

            Panel pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(253, 238, 204) };
            Label lblTitle = new Label { Text = "ĐĂNG NHẬP", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 83, 9), AutoSize = true, Location = new Point(40, 55) };
            Label lblUser = new Label { Text = "Tên đăng nhập:", Font = new Font("Segoe UI", 9.5F), Location = new Point(40, 120), AutoSize = true };
            txtTenDangNhap = new TextBox { Location = new Point(40, 145), Width = 340, Font = new Font("Segoe UI", 11F), Text = "admin" };

            Label lblPass = new Label { Text = "Mật khẩu:", Font = new Font("Segoe UI", 9.5F), Location = new Point(40, 195), AutoSize = true };
            txtMatKhau = new TextBox { Location = new Point(40, 220), Width = 340, Font = new Font("Segoe UI", 11F), PasswordChar = '•', Text = "123" };

            chkHienMatKhau = new CheckBox { Text = "Hiển thị mật khẩu", Location = new Point(40, 260), AutoSize = true };
            chkHienMatKhau.CheckedChanged += (s, e) => { txtMatKhau.PasswordChar = chkHienMatKhau.Checked ? '\0' : '•'; };

            btnDangNhap = new Button
            {
                Text = "ĐĂNG NHẬP",
                Location = new Point(40, 315),
                Width = 220,
                Height = 42,
                BackColor = Color.FromArgb(234, 88, 12),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnDangNhap.FlatAppearance.BorderSize = 0;
            btnDangNhap.Click += BtnDangNhap_Click;

            btnThoat = new Button
            {
                Text = "THOÁT",
                Location = new Point(275, 315),
                Width = 105,
                Height = 42,
                BackColor = Color.FromArgb(148, 163, 184),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnThoat.FlatAppearance.BorderSize = 0;
            btnThoat.Click += (s, e) => Application.Exit();

            pnlRight.Controls.AddRange(new Control[] { lblTitle, lblUser, txtTenDangNhap, lblPass, txtMatKhau, chkHienMatKhau, btnDangNhap, btnThoat });

            this.Controls.Add(pnlRight);
            this.Controls.Add(pnlLeft);
        }

        private async void BtnDangNhap_Click(object? sender, EventArgs e)
        {
            string tk = txtTenDangNhap.Text.Trim();
            string mk = txtMatKhau.Text.Trim();
            if (string.IsNullOrWhiteSpace(tk) || string.IsNullOrWhiteSpace(mk))
            {
                MessageBox.Show("Vui lòng nhập tài khoản và mật khẩu.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnDangNhap.Enabled = false;
            try
            {
                using HttpClient client = new() { BaseAddress = new Uri("http://127.0.0.1:5000") };
                using HttpResponseMessage response = await client.PostAsJsonAsync("/api/dang-nhap",
                    new { username = tk, password = mk });
                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        "Sai tài khoản hoặc mật khẩu!\n\nTài khoản mẫu:\n- Cửa hàng trưởng: admin / 123\n- Trưởng ca: truongca / 123\n- Nhân viên kho: kho / 123\n- Thu ngân: nv / 123",
                        "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show("Không kết nối được API tại cổng 5000. Hãy chạy API trước khi đăng nhập.",
                    "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDangNhap.Enabled = true;
            }
        }

        private sealed record LoginResponse(string EmployeeId, string DisplayName, string Role);
    }
}
