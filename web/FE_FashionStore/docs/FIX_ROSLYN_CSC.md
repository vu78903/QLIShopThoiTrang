# Sửa lỗi MVC thiếu bin/roslyn/csc.exe

Ảnh lỗi được cung cấp ngày 15/10/2026 cho thấy ASP.NET MVC không tìm thấy `FashionStore.Web\bin\roslyn\csc.exe` khi biên dịch ứng dụng. Đây là lỗi đã quan sát trên IIS Express/Windows của người dùng, không phải kết quả chạy Razor trong môi trường tạo source.

Kiểm tra package CodeDom 2.0.1 thực tế từ NuGet xác nhận:

- Có `tools/RoslynLatest/csc.exe`, phù hợp target .NET Framework 4.8 hiện tại.
- `build/net46/Microsoft.CodeDom.Providers.DotNetCompilerPlatform.props` có target `CopyRoslynCompilerFilesToOutputDirectory`, chạy sau `CopyFilesToOutputDirectory`, chép compiler vào bin/roslyn.
- Project đã có import đúng file props nhưng `EnsureNuGetPackageBuildImports` bị tắt bằng `Condition="false"`. Đã bỏ điều kiện này để build báo lỗi thiếu gói sớm.

## Sửa bản đã giải nén trên máy bạn

1. Dừng F5 trong Visual Studio.
2. Mở **Tools → NuGet Package Manager → Package Manager Console**.
3. Chạy:

```powershell
Update-Package Microsoft.CodeDom.Providers.DotNetCompilerPlatform -Reinstall -ProjectName FashionStore.Web
```

4. Chọn **Build → Rebuild Solution**.
5. Xác nhận `FashionStore.Web\bin\roslyn\csc.exe` tồn tại, chạy F5 lại.

Nếu NuGet hoặc Rebuild có lỗi, giữ nguyên toàn bộ thông báo của bước đó để xử lý tiếp. Không chép một csc.exe lẻ từ phiên bản khác hoặc bỏ cấu hình CodeDom để che lỗi.

Target kiểm tra NuGet có thể sửa ngay trong csproj hiện tại nếu bạn không muốn giải nén lại toàn bộ source: tìm target `EnsureNuGetPackageBuildImports`, bỏ riêng thuộc tính `Condition="false"`. Không thay Web.config hoặc connection string trên máy bạn.

## Phạm vi kiểm chứng

Đã đối chiếu cấu trúc package và sửa/parse project. Kiểm tra guard bằng MSBuild được ghi trong `evidence/roslyn-build-guard.json`. Chưa chạy lại IIS Express hoặc tác vụ chép compiler của package trên Windows; bước Reinstall → Rebuild → F5 của người dùng là xác nhận runtime cần thiết. Các test API/C# trước đó không thay thế bước này.
