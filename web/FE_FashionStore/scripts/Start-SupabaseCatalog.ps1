param([int]$Port = 5000, [switch]$EnableCustomerFeatures, [switch]$EnableOrders, [string]$OrderEmployeeId = "NV01")
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\FashionStore.Backend\FashionStore.Backend.csproj'
$previousProvider = $env:Database__Provider
$previousConnection = $env:ConnectionStrings__Supabase
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousOrders = $env:Database__EnableSupabaseOrders
$previousEmployee = $env:Orders__EmployeeId
$previousCustomerFeatures = $env:Database__EnableSupabaseCustomerFeatures
try {
    $connection = $env:ConnectionStrings__Supabase
    if ([string]::IsNullOrWhiteSpace($connection)) { $connection = $env:YODYSTORE_CONNECTION_STRING }
    if ([string]::IsNullOrWhiteSpace($connection)) {
        $connection = [Environment]::GetEnvironmentVariable('ConnectionStrings__Supabase', 'User')
    }
    if ([string]::IsNullOrWhiteSpace($connection)) {
        $connection = [Environment]::GetEnvironmentVariable('YODYSTORE_CONNECTION_STRING', 'User')
    }
    if ([string]::IsNullOrWhiteSpace($connection)) {
        $secureConnection = Read-Host 'Paste the PostgreSQL connection string (hidden; not saved)' -AsSecureString
        $connection = [System.Net.NetworkCredential]::new('', $secureConnection).Password
        $secureConnection.Dispose()
    }
    if ([string]::IsNullOrWhiteSpace($connection)) { throw 'A PostgreSQL connection string is required.' }
    $env:Database__Provider = 'Supabase'
    $env:ConnectionStrings__Supabase = $connection
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Database__EnableSupabaseCustomerFeatures = ($EnableCustomerFeatures.IsPresent -or $EnableOrders.IsPresent).ToString()
    $env:Database__EnableSupabaseOrders = $EnableOrders.IsPresent.ToString()
    $env:Orders__EmployeeId = $OrderEmployeeId
    Write-Host "Starting Supabase backend at http://127.0.0.1:$Port"
    if ($EnableCustomerFeatures -or $EnableOrders) {
        Write-Host 'Customer accounts and carts enabled. Apply database/Supabase-Customer-Cart.sql first.'
    } else { Write-Host 'Read-only catalog mode. Customer accounts and carts are disabled.' }
    if ($EnableOrders) { Write-Host "COD orders enabled for employee $OrderEmployeeId. Apply database/Supabase-Web-Orders.sql first." }
    else { Write-Host 'Checkout is disabled.' }
    Write-Host 'Recovery and admin endpoints are not yet migrated.'
    dotnet run --project $projectPath --no-launch-profile --urls "http://127.0.0.1:$Port"
    if ($LASTEXITCODE -ne 0) { throw 'Backend failed. Check the build/startup output above.' }
}
finally {
    $env:Database__Provider = $previousProvider
    $env:ConnectionStrings__Supabase = $previousConnection
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:Database__EnableSupabaseCustomerFeatures = $previousCustomerFeatures
    $env:Database__EnableSupabaseOrders = $previousOrders
    $env:Orders__EmployeeId = $previousEmployee
    $connection = $null
}
