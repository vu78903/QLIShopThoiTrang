param(
    [string]$ApiUrl = "http://127.0.0.1:5000",
    [string]$AssetRoot = (Join-Path $PSScriptRoot "..\winform\qlicuahang\assets\products"),
    [int]$DefaultStock = 10,
    [string]$StartProductId = "",
    [switch]$LegacyOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$supabaseUrl = [string]$env:SUPABASE_URL
$serviceRoleKey = [string]$env:SUPABASE_SERVICE_ROLE_KEY
if ([string]::IsNullOrWhiteSpace($supabaseUrl) -or [string]::IsNullOrWhiteSpace($serviceRoleKey)) {
    throw "Thiếu SUPABASE_URL hoặc SUPABASE_SERVICE_ROLE_KEY."
}
$supabaseUrl = $supabaseUrl.TrimEnd("/")

$catalog = @(
    @{ Id = "SP-001"; Colors = @("Đen", "Trắng", "Xám", "Đỏ", "Xanh dương"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-002"; Colors = @("Trắng", "Xanh dương", "Đen", "Be", "Hồng"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-003"; Colors = @("Đen", "Trắng", "Xanh navy", "Đỏ", "Xám"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-004"; Colors = @("Đen", "Xanh navy", "Xám", "Đỏ", "Be"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-005"; Colors = @("Đen", "Trắng", "Xám", "Đỏ", "Xanh navy"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-006"; Colors = @("Đen", "Xám", "Xanh navy", "Nâu", "Be"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-007"; Colors = @("Be", "Đen", "Nâu", "Xám", "Xanh navy"); Sizes = @("28", "30", "32", "34", "36") },
    @{ Id = "SP-008"; Colors = @("Xanh dương", "Xanh đậm", "Đen", "Xám", "Trắng"); Sizes = @("28", "30", "32", "34", "36") },
    @{ Id = "SP-009"; Colors = @("Be", "Đen", "Nâu", "Xám", "Xanh navy"); Sizes = @("28", "30", "32", "34", "36") },
    @{ Id = "SP-010"; Colors = @("Xanh dương", "Xanh đậm", "Đen", "Xám", "Trắng"); Sizes = @("28", "30", "32", "34", "36") },
    @{ Id = "SP-011"; Colors = @("Đen", "Xám", "Xanh navy", "Be", "Nâu"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-012"; Colors = @("Đen", "Nâu", "Xám", "Vàng", "Xanh navy"); Sizes = @("Free size") },
    @{ Id = "SP-013"; Colors = @("Đen", "Nâu", "Xám", "Vàng", "Xanh navy"); Sizes = @("Free size") },
    @{ Id = "SP-014"; Colors = @("Đen", "Trắng", "Be", "Xanh navy", "Đỏ"); Sizes = @("S", "M", "L", "XL", "Free size") },
    @{ Id = "SP-015"; Colors = @("Đen", "Nâu", "Be", "Xám", "Xanh navy"); Sizes = @("80", "85", "90", "95", "100") },
    @{ Id = "SP-016"; Colors = @("Trắng", "Đen", "Xám", "Xanh navy", "Be"); Sizes = @("35-38", "39-42", "43-46") },
    @{ Id = "SP-017"; Colors = @("Đen", "Be", "Trắng", "Đỏ", "Xanh navy"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-018"; Colors = @("Đen", "Be", "Trắng", "Đỏ", "Xanh navy"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-019"; Colors = @("Đen", "Be", "Trắng", "Đỏ", "Xanh navy"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-020"; Colors = @("Đen", "Be", "Trắng", "Đỏ", "Xanh navy"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-021"; Colors = @("Trắng", "Đen", "Be", "Hồng", "Xanh dương"); Sizes = @("S", "M", "L", "XL", "XXL") },
    @{ Id = "SP-022"; Colors = @("Trắng", "Đen", "Xám", "Be", "Nâu"); Sizes = @("S", "M", "L", "XL", "XXL") }
)

function ConvertTo-Slug([string]$Value) {
    $normalized = $Value.Normalize([Text.NormalizationForm]::FormD)
    $builder = [Text.StringBuilder]::new()
    foreach ($character in $normalized.ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($character) -ne
            [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$builder.Append($character)
        }
    }
    return $builder.ToString().Replace("đ", "d").Replace("Đ", "D").ToLowerInvariant() -replace "[^a-z0-9]+", "-" -replace "^-|-$", ""
}

function Invoke-WithRetry {
    param(
        [scriptblock]$Action,
        [string]$Label,
        [int]$MaxAttempts = 4
    )
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            return & $Action
        }
        catch {
            if ($attempt -eq $MaxAttempts) { throw }
            Write-Warning "$Label thất bại lần $attempt/$MaxAttempts; đang thử lại. $($_.Exception.Message)"
            Start-Sleep -Seconds ([Math]::Pow(2, $attempt - 1))
        }
    }
}

function Invoke-JsonRequest {
    param([string]$Uri, [string]$Method = "Get", [object]$Body)
    $parameters = @{
        Uri = $Uri
        Method = $Method
        ContentType = "application/json; charset=utf-8"
    }
    if ($PSBoundParameters.ContainsKey("Body")) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 8 -Compress
    }
    return Invoke-WithRetry -Label "$Method $Uri" -Action { Invoke-RestMethod @parameters }
}

$state = Invoke-JsonRequest -Uri "$ApiUrl/api/app-state"
$products = @($state.productsData)
$storageHeaders = @{
    Authorization = "Bearer $serviceRoleKey"
    apikey = $serviceRoleKey
}

try {
    Invoke-RestMethod -Uri "$supabaseUrl/storage/v1/bucket/product-images" -Method Get -Headers $storageHeaders | Out-Null
}
catch {
    $bucketBody = @{
        id = "product-images"
        name = "product-images"
        public = $true
        file_size_limit = 5MB
        allowed_mime_types = @("image/png", "image/jpeg", "image/webp")
    } | ConvertTo-Json -Compress
    Invoke-RestMethod -Uri "$supabaseUrl/storage/v1/bucket" -Method Post -Headers $storageHeaders -ContentType "application/json" -Body $bucketBody | Out-Null
}

# Nâng giới hạn bucket để nhận ảnh AI gốc; ứng dụng vẫn kiểm soát ảnh người dùng ở tầng API.
try {
    $bucketUpdate = @{
        public = $true
        file_size_limit = 5MB
        allowed_mime_types = @("image/png", "image/jpeg", "image/webp")
    } | ConvertTo-Json -Compress
    Invoke-RestMethod -Uri "$supabaseUrl/storage/v1/bucket/product-images" -Method Put -Headers $storageHeaders -ContentType "application/json" -Body $bucketUpdate | Out-Null
}
catch {
    Write-Warning "Không thể cập nhật giới hạn bucket; sẽ tiếp tục và báo lỗi nếu ảnh vượt giới hạn hiện tại."
}

$uploadedImages = 0
$savedVariants = 0
$started = [string]::IsNullOrWhiteSpace($StartProductId)
foreach ($entry in $catalog) {
    if (-not $started) {
        $started = $entry.Id -eq $StartProductId
        if (-not $started) { continue }
    }
    $representative = $products | Where-Object productId -EQ $entry.Id | Select-Object -First 1
    if ($null -eq $representative) {
        Write-Warning "Bỏ qua $($entry.Id) vì không còn tồn tại trong dữ liệu."
        continue
    }

    if (-not $LegacyOnly) {
        foreach ($color in $entry.Colors) {
            $slug = ConvertTo-Slug $color
            $imagePath = Join-Path $AssetRoot "$($entry.Id)\$slug.png"
            if (-not (Test-Path -LiteralPath $imagePath)) {
                throw "Không tìm thấy ảnh: $imagePath"
            }

            $objectPath = "catalog/$($entry.Id)/$slug.png"
            $uploadHeaders = @{
                Authorization = "Bearer $serviceRoleKey"
                apikey = $serviceRoleKey
                "x-upsert" = "true"
            }
            Invoke-WithRetry -Label "Tải ảnh $($entry.Id)/$slug" -Action {
                Invoke-RestMethod -Uri "$supabaseUrl/storage/v1/object/product-images/$objectPath" -Method Post `
                    -Headers $uploadHeaders -ContentType "image/png" -InFile $imagePath | Out-Null
            }
            $imageUrl = "$supabaseUrl/storage/v1/object/public/product-images/$objectPath"
            $uploadedImages++

            foreach ($size in $entry.Sizes) {
                $existing = $products | Where-Object {
                    $_.productId -eq $entry.Id -and
                    $_.color -eq $color -and
                    $_.size -eq $size
                } | Select-Object -First 1

                $body = @{
                    productId = $entry.Id
                    sku = if ($null -ne $existing) { $existing.sku } else { $null }
                    name = $representative.name
                    type = $representative.type
                    price = $representative.price
                    stock = if ($null -ne $existing) { $existing.stock } else { $DefaultStock }
                    color = $color
                    size = $size
                    image = $imageUrl
                }
                Invoke-JsonRequest -Uri "$ApiUrl/api/san-pham" -Method Post -Body $body | Out-Null
                $savedVariants++
            }
        }
    }

    # Giữ các biến thể cũ ngoài bộ size chuẩn, nhưng vẫn gắn ảnh cùng màu để không làm mất dữ liệu người dùng.
    $legacyVariants = $products | Where-Object {
        $_.productId -eq $entry.Id -and
        ($entry.Colors -notcontains $_.color -or $entry.Sizes -notcontains $_.size)
    }
    foreach ($legacy in $legacyVariants) {
        $imageColor = if ($entry.Id -eq "SP-010" -and $legacy.color -eq "Xanh") { "Xanh dương" } else { $legacy.color }
        $legacySlug = ConvertTo-Slug $imageColor
        $legacyImagePath = Join-Path $AssetRoot "$($entry.Id)\$legacySlug.png"
        if (-not (Test-Path -LiteralPath $legacyImagePath)) {
            Write-Warning "Chưa có ảnh phù hợp cho biến thể cũ $($legacy.sku)."
            continue
        }

        $legacyImageUrl = "$supabaseUrl/storage/v1/object/public/product-images/catalog/$($entry.Id)/$legacySlug.png"
        $legacyBody = @{
            productId = $legacy.productId
            sku = $legacy.sku
            name = $legacy.name
            type = $legacy.type
            price = $legacy.price
            stock = $legacy.stock
            color = $legacy.color
            size = $legacy.size
            image = $legacyImageUrl
        }
        Invoke-JsonRequest -Uri "$ApiUrl/api/san-pham" -Method Post -Body $legacyBody | Out-Null
        $savedVariants++
    }
    Write-Host "Đã đồng bộ $($entry.Id): $($entry.Colors.Count) màu."
}

[pscustomobject]@{
    Products = $catalog.Count
    Images = $uploadedImages
    Variants = $savedVariants
} | ConvertTo-Json -Compress
