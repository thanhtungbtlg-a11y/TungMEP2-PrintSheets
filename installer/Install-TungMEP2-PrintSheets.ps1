[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Hay dong Revit truoc khi cai PrintSheets.' }
$packageRoot = $PSScriptRoot
$sourceBin = Join-Path $packageRoot 'net48'
$sourceDll = Join-Path $sourceBin 'TungMEP2.PrintSheets.dll'

if (-not (Test-Path -LiteralPath $sourceDll)) {
    throw "Khong tim thay file add-in: $sourceDll"
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDll).Hash
$buildId = $hash.Substring(0, 12)
$localAppData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::LocalApplicationData)
$appData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::ApplicationData)
$installRoot = Join-Path $localAppData 'TungMEP2-PrintSheets\Revit2024'
$installDirectory = Join-Path $installRoot $buildId
$installBin = Join-Path $installDirectory 'net48'
$addinsDirectory = Join-Path $appData 'Autodesk\Revit\Addins\2024'
$manifestPath = Join-Path $addinsDirectory 'TungMEP2-PrintSheets.addin'

[IO.Directory]::CreateDirectory($installBin) | Out-Null
Get-ChildItem -LiteralPath $sourceBin -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $installBin -Force
}
[IO.Directory]::CreateDirectory($addinsDirectory) | Out-Null

$installedDll = Join-Path $installBin 'TungMEP2.PrintSheets.dll'
$escapedDll = [Security.SecurityElement]::Escape($installedDll)
if (Test-Path -LiteralPath $manifestPath) {
    $backupDirectory = Join-Path $installRoot ('manifest-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
    Copy-Item -LiteralPath $manifestPath -Destination $backupDirectory
}
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>TungMEP2 PrintSheets</Name>
    <Assembly>$escapedDll</Assembly>
    <AddInId>8E5C88F4-0C70-4CB7-A2CF-C792E7BF762D</AddInId>
    <FullClassName>TungMEP2.PrintSheets.Application</FullClassName>
    <VendorId>TM2P</VendorId>
    <VendorDescription>PrintSheets 2026.10.03.1 - Revit 2024</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
[IO.File]::WriteAllText(
    $manifestPath,
    $manifest,
    [Text.UTF8Encoding]::new($false))

Write-Host 'Da cai TungMEP2 PrintSheets cho Revit 2024.' -ForegroundColor Green
Write-Host "Add-in: $manifestPath"
Write-Host "Files : $installDirectory"
Write-Host 'Dong va mo lai Revit neu Revit dang chay.'
