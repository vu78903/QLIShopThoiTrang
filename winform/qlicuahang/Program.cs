using System;
using System.IO;
using System.Windows.Forms;

namespace qlicuahang
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "startup.log");

            try
            {
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Bat dau ung dung.{Environment.NewLine}");
                ApplicationConfiguration.Initialize();

                bool apiReady = ApiProcessManager.EnsureRunningAsync().GetAwaiter().GetResult();
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} API san sang: {apiReady}.{Environment.NewLine}");
                if (!apiReady)
                {
                    MessageBox.Show(
                        "Không thể kết nối API tại cổng 5000. Hãy kiểm tra Internet, Supabase hoặc tiến trình đang dùng cổng 5000.",
                        "Lỗi khởi động", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Mở form đăng nhập trước. Đăng nhập đúng thì mới cho vào màn hình chính.
                using formDangNhap login = new();
                DialogResult loginResult = login.ShowDialog();
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Dong form dang nhap: {loginResult}.{Environment.NewLine}");
                if (loginResult == DialogResult.OK)
                    Application.Run(new formTongQuan());
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Loi khoi dong: {ex}{Environment.NewLine}");
                MessageBox.Show(
                    $"Ứng dụng không thể khởi động. Chi tiết đã được ghi tại:{Environment.NewLine}{logPath}{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "Lỗi khởi động", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
