[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# Canonical manual-STT maintenance source; do not rebuild the archived responsive engine.
& (Join-Path $PSScriptRoot 'Build-Maintenance.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Verified maintenance build failed.' }
