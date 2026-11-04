namespace QLIShopThoiTrang.Api.Data;

// Lỗi dữ liệu nghiệp vụ do người nhập, tách khỏi lỗi kết nối hoặc dịch vụ ảnh.
public sealed class StoreValidationException(string message) : InvalidOperationException(message);
