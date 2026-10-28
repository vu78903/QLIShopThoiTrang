$ErrorActionPreference = 'Stop'

function Read-SecretValue([string]$Prompt) {
    $secureValue = Read-Host $Prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

Write-Host '======================================================' -ForegroundColor Cyan
Write-Host '  CẤU HÌNH THANH TOÁN QR ĐỘNG PAYOS CHO WINFORMS' -ForegroundColor Cyan
Write-Host '======================================================' -ForegroundColor Cyan
Write-Host 'Các ký tự nhập vào sẽ được ẩn và không ghi vào project.'
Write-Host

$clientId = Read-SecretValue 'Nhập Client ID'
$apiKey = Read-SecretValue 'Nhập API Key'
$checksumKey = Read-SecretValue 'Nhập Checksum Key'

if ([string]::IsNullOrWhiteSpace($clientId) -or
    [string]::IsNullOrWhiteSpace($apiKey) -or
    [string]::IsNullOrWhiteSpace($checksumKey)) {
    throw 'Cả ba khóa payOS đều bắt buộc.'
}

[Environment]::SetEnvironmentVariable('PAYOS_CLIENT_ID', $clientId, 'User')
[Environment]::SetEnvironmentVariable('PAYOS_API_KEY', $apiKey, 'User')
[Environment]::SetEnvironmentVariable('PAYOS_CHECKSUM_KEY', $checksumKey, 'User')

$clientId = $null
$apiKey = $null
$checksumKey = $null

Write-Host
Write-Host 'Đã cấu hình xong. Hãy đóng hoàn toàn Visual Studio và WinForms rồi mở lại.' -ForegroundColor Green
Write-Host 'Tài khoản nhận tiền được lấy từ kênh ngân hàng đã liên kết trên payOS.' -ForegroundColor Green
