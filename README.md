# QLIShopThoiTrang - Huong dan chay project

## Co so du lieu

Project dang dung PostgreSQL tren Supabase. Khong con dung SQL Server hoac SQLite.

Thu muc `database` chi giu:

- `YodyStore_AddGiaoHang_Supabase.sql`: script bo sung cot giao hang tren Supabase.
- `san-pham-import-test.csv`: du lieu mau de thu chuc nang import san pham.

Khong sua file SQL de thay doi du lieu dang chay. Them, sua, xoa du lieu qua form, API hoac Table Editor cua Supabase.

## Cau hinh ket noi

API doc chuoi ket noi tu bien moi truong `YODYSTORE_CONNECTION_STRING`.

Trong PowerShell:

```powershell
[Environment]::SetEnvironmentVariable(
  "YODYSTORE_CONNECTION_STRING",
  "CHUOI_KET_NOI_SUPABASE",
  "User"
)
```

Dong va mo lai Visual Studio sau khi dat bien moi truong.

## Chay ung dung

Mo `winform/qlicuahang/qlicuahang.csproj` bang Visual Studio va bam Start. WinForms se tu khoi dong API tai `http://127.0.0.1:5000`.

API chinh:

- `GET /api/health`: kiem tra ket noi Supabase.
- `GET /api/app-state`: lay du lieu cho cac form.
- `POST /api/san-pham`: them hoac sua san pham.
- `DELETE /api/san-pham/{sku}`: xoa san pham hop le.
