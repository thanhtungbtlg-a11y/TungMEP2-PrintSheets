[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Close Revit before unregistering PrintSheets.' }
$manifestPath = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024\TungMEP2-PrintSheets.addin'
if (Test-Path -LiteralPath $manifestPath) {
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    if ([string]$manifest.RevitAddIns.AddIn.VendorDescription -eq 'PrintSheets 2026.10.03.1 - Revit 2024') {
        $backup = Join-Path $env:LOCALAPPDATA ('TungMEP2-PrintSheets\Revit2024\manifest-backups\unregister-' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($backup) | Out-Null
        Move-Item -LiteralPath $manifestPath -Destination $backup
        Write-Host "Unregistered. Manifest backup: $backup"
    } else { Write-Host 'A different version is registered; left it unchanged.' }
}
Write-Host 'Previous binaries/backups are retained; no installation folders were deleted.'
