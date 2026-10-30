# Changelog Task 3

## Phase 1 — Foundation

Audit và plan đã lập trước thay đổi source. Giữ layout Mộc Nhiên; thêm navigation tài khoản, login/logout theo phiên, badge giỏ, stylesheet scope .customer-site, responsive và focus states; khai báo customer.css và file mới trong csproj. Chưa nghiệm thu viewport runtime.

## Phase 2 — Shopping

Catalog lọc size/màu/giá/tồn trên cùng SKU và trước paging SQL. Chi tiết có giới hạn số lượng theo SKU, related products và đường dẫn reviews/wishlist. Cart có ảnh. Checkout có voucher preview và server recalculation; giữ RequestId, pending payment, không trừ kho khi tạo. Orders có success message, trạng thái Việt hóa, filter/paging SQL, timeline và hủy qua procedure có khóa ownership. Cần test SQL regression.

## Phase 3 — Account

Dashboard, profile navigation, sổ địa chỉ lưu theo tài khoản và chọn tại checkout. Khôi phục password bằng token hash, expiry 20 phút, one-use transaction, SMTP cấu hình thật; thiếu config báo rõ. Unified login và Admin-specific files giữ nguyên. SMTP/runtime chưa thử.

## Phase 4 — Business

Khuyến mãi hợp lệ chung, ledger điểm thật, gửi đổi/trả pending có ownership/15 ngày/cumulative quantity, review xác minh đơn hoàn tất và chờ duyệt, wishlist theo tài khoản. Migration DEV thêm ba bảng Web-only. Không tự đặt quy đổi điểm hay sửa procedure tài chính. Các phần chưa hoàn chỉnh được liệt kê trong test report.

## Phase 5 — Finalization

Trang hỗ trợ/chính sách, liên hệ/ngân hàng đọc cấu hình. Static checks có kết quả riêng; không giả lập build/UI PASS. Đóng gói sạch giữ solution/project/source. Không sửa ZIP đầu vào.

## Danh sách file tạo/sửa

- Tạo: `FashionStore.Backend/Controllers/CustomerFeaturesController.cs`
- Sửa: `FashionStore.Backend/Controllers/OrdersController.cs`
- Tạo: `FashionStore.Backend/Controllers/RecoveryController.cs`
- Sửa: `FashionStore.Backend/DTOs/CatalogDtos.cs`
- Sửa: `FashionStore.Backend/DTOs/CustomerDtos.cs`
- Sửa: `FashionStore.Backend/DTOs/OrderDtos.cs`
- Sửa: `FashionStore.Backend/Middleware/ApiExceptionHandler.cs`
- Sửa: `FashionStore.Backend/Program.cs`
- Sửa: `FashionStore.Backend/Services/CartService.cs`
- Sửa: `FashionStore.Backend/Services/CatalogService.cs`
- Tạo: `FashionStore.Backend/Services/CustomerFeaturesService.cs`
- Sửa: `FashionStore.Backend/Services/OrderService.cs`
- Tạo: `FashionStore.Backend/Services/PasswordRecoveryService.cs`
- Tạo: `FashionStore.Backend.Tests/CustomerTask3Tests.cs`
- Sửa: `FashionStore.Web/Content/customer.css`
- Sửa: `FashionStore.Web/Controllers/AccountController.cs`
- Sửa: `FashionStore.Web/Controllers/CartController.cs`
- Sửa: `FashionStore.Web/Controllers/CheckoutController.cs`
- Tạo: `FashionStore.Web/Controllers/CustomerController.cs`
- Sửa: `FashionStore.Web/Controllers/HomeController.cs`
- Sửa: `FashionStore.Web/Controllers/OrdersController.cs`
- Sửa: `FashionStore.Web/Controllers/ProductController.cs`
- Sửa: `FashionStore.Web/FashionStore.Web.csproj`
- Tạo: `FashionStore.Web/Helpers/CustomerViewHelper.cs`
- Sửa: `FashionStore.Web/Models/CatalogModels.cs`
- Sửa: `FashionStore.Web/Models/CustomerModels.cs`
- Sửa: `FashionStore.Web/Models/OrderModels.cs`
- Sửa: `FashionStore.Web/Scripts/moc-nhien.js`
- Sửa: `FashionStore.Web/Services/CatalogRepository.cs`
- Sửa: `FashionStore.Web/Services/CustomerApiClient.cs`
- Tạo: `FashionStore.Web/Views/Account/Forgot.cshtml`
- Sửa: `FashionStore.Web/Views/Account/Login.cshtml`
- Sửa: `FashionStore.Web/Views/Account/Password.cshtml`
- Sửa: `FashionStore.Web/Views/Account/Profile.cshtml`
- Tạo: `FashionStore.Web/Views/Account/Reset.cshtml`
- Sửa: `FashionStore.Web/Views/Cart/Index.cshtml`
- Sửa: `FashionStore.Web/Views/Checkout/Index.cshtml`
- Tạo: `FashionStore.Web/Views/Customer/Addresses.cshtml`
- Tạo: `FashionStore.Web/Views/Customer/Index.cshtml`
- Tạo: `FashionStore.Web/Views/Customer/Points.cshtml`
- Tạo: `FashionStore.Web/Views/Customer/Promotions.cshtml`
- Tạo: `FashionStore.Web/Views/Customer/Return.cshtml`
- Tạo: `FashionStore.Web/Views/Customer/Returns.cshtml`
- Tạo: `FashionStore.Web/Views/Customer/Reviews.cshtml`
- Tạo: `FashionStore.Web/Views/Customer/Wishlist.cshtml`
- Sửa: `FashionStore.Web/Views/Home/Contact.cshtml`
- Sửa: `FashionStore.Web/Views/Home/Index.cshtml`
- Tạo: `FashionStore.Web/Views/Home/Policy.cshtml`
- Sửa: `FashionStore.Web/Views/Orders/Details.cshtml`
- Sửa: `FashionStore.Web/Views/Orders/Index.cshtml`
- Sửa: `FashionStore.Web/Views/Product/Details.cshtml`
- Sửa: `FashionStore.Web/Views/Product/Index.cshtml`
- Tạo: `FashionStore.Web/Views/Shared/_CustomerMessages.cshtml`
- Tạo: `FashionStore.Web/Views/Shared/_CustomerNav.cshtml`
- Tạo: `FashionStore.Web/Views/Shared/_FeaturePaging.cshtml`
- Sửa: `FashionStore.Web/Views/Shared/_Layout.cshtml`
- Sửa: `FashionStore.Web/Views/Shared/_ProductCard.cshtml`
- Tạo: `README_TASK3.md`
- Tạo: `database/06_CustomerFeatures_Dev.sql`
- Tạo: `database/06_CustomerFeatures_Rollback_Dev.sql`
- Tạo: `docs/CUSTOMER_API_MAPPING.md`
- Tạo: `docs/CUSTOMER_RUN_GUIDE.md`
- Tạo: `docs/CUSTOMER_SOURCE_AUDIT.md`
- Tạo: `docs/CUSTOMER_STATIC_CHECKS.json`
- Tạo: `docs/CUSTOMER_TEST_REPORT.md`
- Tạo: `docs/CUSTOMER_UI_IMPLEMENTATION_PLAN.md`
- Tạo: `docs/CUSTOMER_UI_CHANGELOG.md` (tài liệu này).
