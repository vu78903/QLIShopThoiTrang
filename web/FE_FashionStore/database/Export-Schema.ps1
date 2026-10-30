#Requires -Version 5.1
<#
Run in Windows PowerShell 5.1. Reads metadata from an ALREADY restored DEV copy.
Does not restore databases, install procedures, migrate, seed, or read customer rows.
SQL Authentication is optional through -Credential (Get-Credential).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerInstance,

    [Parameter(Mandatory = $true)]
    [ValidateSet('FashionStoreDB_Dev', 'YodyStore_Dev')]
    [string]$Database,

    [string]$OutputDirectory,

    [System.Management.Automation.PSCredential]$Credential,

    # Opt in only for a local development instance with a self-signed certificate.
    [switch]$TrustServerCertificate
)

$ErrorActionPreference = 'Stop'
# Resolve script-relative defaults after parameter binding (Windows PowerShell 5.1).
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path -Path $PSScriptRoot -ChildPath 'exports'
}
Add-Type -AssemblyName System.Data

$settings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
# Use SQL keyword keys: PowerShell's IDictionary adapter intercepts property setters.
$settings['Data Source'] = $ServerInstance
$settings['Initial Catalog'] = $Database
$settings['Application Name'] = 'FashionStore Schema Audit ReadOnly'
$settings['Connect Timeout'] = 15
$settings['Encrypt'] = $true
$settings['TrustServerCertificate'] = $TrustServerCertificate.IsPresent
if ($null -eq $Credential) {
    $settings['Integrated Security'] = $true
} else {
    $settings['Integrated Security'] = $false
    $settings['User ID'] = $Credential.UserName
    $settings['Password'] = $Credential.GetNetworkCredential().Password
}

$connection = New-Object System.Data.SqlClient.SqlConnection($settings.get_ConnectionString())
# Do not log the connection string or credential.
$settings.Clear()
$command = $null
try {
    $connection.Open()
    if ($connection.Database -cne $Database) {
        throw 'Connected database does not match the requested DEV copy.'
    }
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 60
    $command.CommandText = [IO.File]::ReadAllText(
        (Join-Path $PSScriptRoot 'Export-Schema-ReadOnly.sql'), [Text.Encoding]::UTF8)
    $json = [string]$command.ExecuteScalar()
    $metadata = $json | ConvertFrom-Json
    if ($metadata.databaseName -cne $Database -or $metadata.exportFormatVersion -ne 1) {
        throw 'Unexpected schema export. No output written.'
    }
    $null = New-Item -ItemType Directory -Force -Path $OutputDirectory
    $filename = '{0}_schema_{1}_{2}.json' -f $Database,
        [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss_fff'), [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $outputPath = Join-Path (Resolve-Path -LiteralPath $OutputDirectory).Path $filename
    [IO.File]::WriteAllText($outputPath, $json, (New-Object Text.UTF8Encoding($false)))
    Write-Output ('Schema export: ' + $outputPath)
    $metadata.counts | Format-List | Out-Host
    if ($metadata.counts.unavailableModuleDefinitions -gt 0) {
        Write-Warning 'Some module definitions are unavailable/encrypted. Export is incomplete for those modules.'
    }
    Write-Output 'Metadata exported. This is NOT a database audit PASS or an application test PASS.'
} finally {
    if ($null -ne $command) { $command.Dispose() }
    $connection.Dispose()
}
