#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ServerInstance = '.\SQLEXPRESS',
    [System.Management.Automation.PSCredential]$Credential,
    [switch]$TrustServerCertificate
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$settings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
# Use SQL keyword keys: PowerShell's IDictionary adapter intercepts property setters.
$settings['Data Source'] = $ServerInstance
$settings['Initial Catalog'] = 'FashionStoreDB_Dev'
if ($null -eq $Credential) {
    $settings['Integrated Security'] = $true
} else {
    $settings['Integrated Security'] = $false
    $settings['User ID'] = $Credential.UserName
    $settings['Password'] = $Credential.GetNetworkCredential().Password
}
$settings['Encrypt'] = $true
$settings['TrustServerCertificate'] = $TrustServerCertificate.IsPresent
$settings['Connect Timeout'] = 5
$previousConnection = $env:FASHIONSTORE_TEST_CONNECTION
$project = Join-Path $PSScriptRoot '..\FashionStore.Backend.Tests\FashionStore.Backend.Tests.csproj'
try {
    $env:FASHIONSTORE_TEST_CONNECTION = $settings.get_ConnectionString()
    Write-Host 'Running tests against the restored FashionStoreDB_Dev copy.'
    Write-Host 'Integration tests insert temporary catalog, customer, account and cart rows, then remove them afterwards.'
    & dotnet test $project --configuration Release --logger 'trx;LogFileName=SqlCatalog.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. Inspect the test output and TRX.' }
} finally {
    $env:FASHIONSTORE_TEST_CONNECTION = $previousConnection
}
