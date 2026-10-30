param(
    [string]$ApiBase = "http://127.0.0.1:5000",
    [string]$ManifestPath = (Join-Path $PSScriptRoot "..\database\catalog-images-2026-10-29.json")
)

$ErrorActionPreference = "Stop"

function Get-ItemFiles($item) {
    if ($item.PSObject.Properties.Name -contains "files" -and @($item.files).Count -gt 0) {
        return @($item.files)
    }
    if ($item.PSObject.Properties.Name -contains "file" -and -not [string]::IsNullOrWhiteSpace($item.file)) {
        return @($item.file)
    }
    throw "Nhóm $($item.id) chưa khai báo file ảnh."
}

function Get-ProductImages($product) {
    $values = @()
    if ($product.PSObject.Properties.Name -contains "images") {
        $values = @($product.images | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    if ($values.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace($product.image)) {
        $values = @($product.image)
    }
    return @($values | Select-Object -Unique)
}

function New-ProductPayload($product, [object[]]$images) {
    return @{
        productId = $product.productId
        sku = $product.sku
        name = $product.name
        type = $product.type
        audience = $product.audience
        supplierId = $product.supplierId
        price = $product.price
        stock = $product.stock
        color = $product.color
        size = $product.size
        image = if ($images.Count -gt 0) { $images[0] } else { $null }
        images = @($images)
    }
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$assetRoot = Join-Path $repoRoot $manifest.assetDirectory
$state = Invoke-RestMethod -Uri "$ApiBase/api/app-state" -TimeoutSec 60
$productsBySku = @{}
foreach ($product in $state.productsData) { $productsBySku[$product.sku] = $product }

$updated = 0
$uploaded = 0
$results = [System.Collections.Generic.List[object]]::new()

foreach ($item in $manifest.items) {
    $files = Get-ItemFiles $item
    if ($files.Count -gt 5) { throw "Nhóm $($item.id) vượt quá giới hạn 5 ảnh." }
    $imagePaths = @($files | ForEach-Object {
        $path = Join-Path $assetRoot $_
        if (-not (Test-Path -LiteralPath $path)) { throw "Không tìm thấy ảnh: $path" }
        $path
    })

    $products = @($item.skus | ForEach-Object {
        if (-not $productsBySku.ContainsKey($_)) { throw "Không tìm thấy SKU trong Supabase: $_" }
        $productsBySku[$_]
    })

    foreach ($product in $products) {
        if ($product.productId -ne $item.productId -or $product.color -ne $item.color) {
            throw "SKU $($product.sku) không khớp Mã SP/màu của manifest $($item.id). Dừng để tránh gán sai ảnh."
        }
        foreach ($field in @("name", "type", "audience")) {
            if ($item.PSObject.Properties.Name -contains $field -and $product.$field -ne $item.$field) {
                throw "SKU $($product.sku) không khớp trường $field của manifest $($item.id)."
            }
        }
    }

    $signatures = @($products | ForEach-Object { (Get-ProductImages $_) -join "|" } | Select-Object -Unique)
    $currentImages = if ($signatures.Count -eq 1) { @(Get-ProductImages $products[0]) } else { @() }
    $hasCompleteGallery = $currentImages.Count -eq $files.Count -and $currentImages.Count -ge 1

    if ($hasCompleteGallery) {
        $imageUrls = $currentImages
    }
    else {
        $dataUrls = @($imagePaths | ForEach-Object {
            $bytes = [System.IO.File]::ReadAllBytes($_)
            if ($bytes.Length -gt 2MB) { throw "Ảnh $_ vượt giới hạn 2 MB." }
            "data:image/jpeg;base64," + [Convert]::ToBase64String($bytes)
        })
        $first = $products[0]
        $payload = New-ProductPayload $first $dataUrls | ConvertTo-Json -Depth 6
        $saved = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/san-pham" -ContentType "application/json; charset=utf-8" -Body $payload -TimeoutSec 300
        $imageUrls = @(Get-ProductImages $saved)
        if ($imageUrls.Count -ne $files.Count) {
            throw "API chỉ trả $($imageUrls.Count)/$($files.Count) ảnh cho $($item.id)."
        }
        $productsBySku[$first.sku] = $saved
        $uploaded += $imageUrls.Count
    }

    $expectedSignature = $imageUrls -join "|"
    foreach ($product in $products) {
        if (((Get-ProductImages $product) -join "|") -eq $expectedSignature) { continue }
        $payload = New-ProductPayload $product $imageUrls | ConvertTo-Json -Depth 6
        $saved = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/san-pham" -ContentType "application/json; charset=utf-8" -Body $payload -TimeoutSec 180
        $productsBySku[$product.sku] = $saved
    }

    $updated += $products.Count
    $results.Add([pscustomobject]@{
        id = $item.id
        productId = $item.productId
        color = $item.color
        skuCount = $products.Count
        imageCount = $imageUrls.Count
        imageUrls = $imageUrls
    })
    Write-Host ("OK {0}: {1} SKU x {2} ảnh" -f $item.id, $products.Count, $imageUrls.Count)
}

$freshState = Invoke-RestMethod -Uri "$ApiBase/api/app-state" -TimeoutSec 60
$freshBySku = @{}
foreach ($product in $freshState.productsData) { $freshBySku[$product.sku] = $product }

$verified = 0
foreach ($item in $manifest.items) {
    $expectedCount = (Get-ItemFiles $item).Count
    $signatures = @($item.skus | ForEach-Object {
        $product = $freshBySku[$_]
        if ($null -eq $product) { throw "Xác minh thất bại: không còn SKU $_." }
        $images = @(Get-ProductImages $product)
        if ($images.Count -ne $expectedCount) {
            throw "Xác minh thất bại: SKU $_ có $($images.Count)/$expectedCount ảnh."
        }
        $verified++
        $images -join "|"
    } | Select-Object -Unique)
    if ($signatures.Count -ne 1) {
        throw "Xác minh thất bại: các size của nhóm $($item.id) chưa dùng chung đúng bộ ảnh."
    }
}

[pscustomobject]@{
    batchId = $manifest.batchId
    uploadedImages = $uploaded
    updatedSkus = $updated
    verifiedSkus = $verified
    groups = $results
} | ConvertTo-Json -Depth 6
