# FashionStore Task 2 — CHECKOUT / ORDER / ADMIN

Đây là source **phát triển tiếp từ chính ZIP Task 1 của bạn**. Không thay tài khoản admin, không ghi đè database thật, không sửa phần xác thực Customer/Admin.

ĐỌC TRƯỚC: `docs/TASK2_CHECKOUT_ORDERS_SETUP.md`

1. Backup `FashionStoreDB_Dev`.
2. Review rồi chạy `database/04_OrderProcedures_Dev_ReviewFirst.sql` trong SSMS (CHỈ DEV).
3. Mở `FashionStore.sln` bằng Visual Studio 2022; restore NuGet và rebuild.
4. Chạy Backend (`http://127.0.0.1:5000`) và Web (`http://localhost:58472` theo cấu hình mặc định).
5. Test Customer: Cart -> Checkout -> Orders.
6. Test Admin: `/Admin/Login` -> `/Admin/Orders` -> Confirm/Cancel.
7. Dùng `database/05_Verify_Order_Dev_ReadOnly.sql` kiểm tra ledger và SQL statuses.

Bản này chưa được chạy Windows build/live SQL Server; cần kiểm chứng trên máy bạn trước khi nghiệm thu.
