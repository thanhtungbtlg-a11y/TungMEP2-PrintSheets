[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# Canonical manual-STT maintenance source; do not rebuild the archived responsive engine.
& (Join-Path $PSScriptRoot 'maintenance\2026.10.03.1\Build-Maintenance.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Verified maintenance build failed.' }
$maintenance = Join-Path $PSScriptRoot 'maintenance\2026.10.03.1'
$publicDist = Join-Path $PSScriptRoot 'dist\2026.10.03.1'
[IO.Directory]::CreateDirectory($publicDist) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot 'release\TungMEP2-PrintSheets')) | Out-Null
Copy-Item -Path (Join-Path $maintenance 'release\TungMEP2-PrintSheets\*') -Destination (Join-Path $PSScriptRoot 'release\TungMEP2-PrintSheets') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $maintenance 'release\TungMEP2-PrintSheets') -Destination $publicDist -Recurse -Force
Copy-Item -Path (Join-Path $maintenance 'dist\*') -Destination $publicDist -Force
Copy-Item -LiteralPath (Join-Path $maintenance 'build\TungMEP2.PrintSheets.dll') -Destination (Join-Path $PSScriptRoot 'build\TungMEP2.PrintSheets.dll') -Force
Write-Host "Published verified standalone packages: $publicDist"
