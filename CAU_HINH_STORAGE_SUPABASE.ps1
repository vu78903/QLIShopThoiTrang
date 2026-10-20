$ErrorActionPreference = "Stop"

$projectUrl = "https://oagcfzcslkkiokbmkrlg.supabase.co"
$secureKey = Read-Host "Dan SUPABASE service_role key MOI vao day" -AsSecureString
$serviceRoleKey = [System.Net.NetworkCredential]::new("", $secureKey).Password

if ([string]::IsNullOrWhiteSpace($serviceRoleKey)) {
    throw "service_role key khong duoc de trong."
}

$isLegacyServiceRole = $false
if ($serviceRoleKey.StartsWith("eyJ")) {
    try {
        $payload = $serviceRoleKey.Split('.')[1].Replace('-', '+').Replace('_', '/')
        while ($payload.Length % 4 -ne 0) { $payload += '=' }
        $claims = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
        $isLegacyServiceRole = $claims.role -eq "service_role"
    }
    catch {
        $isLegacyServiceRole = $false
    }
}

if (-not $isLegacyServiceRole -and -not $serviceRoleKey.StartsWith("sb_secret_")) {
    throw "Gia tri vua dan khong phai Supabase service_role key. Khong co gi duoc luu."
}

[Environment]::SetEnvironmentVariable("SUPABASE_URL", $projectUrl, "User")
[Environment]::SetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY", $serviceRoleKey.Trim(), "User")

$serviceRoleKey = $null
$secureKey.Dispose()

Write-Host "Da luu cau hinh Supabase Storage cho tai khoan Windows hien tai." -ForegroundColor Green
Write-Host "Hay tat hoan toan Visual Studio va ung dung, sau do mo lai." -ForegroundColor Yellow
