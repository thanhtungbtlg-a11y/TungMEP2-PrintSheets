[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$version = '2026.10.03.1'
$standaloneRoot = Split-Path -Parent (Split-Path -Parent $root)
$dotnet = Join-Path (Split-Path -Parent $standaloneRoot) 'tungmep2\.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
& $dotnet build (Join-Path $root 'src\TungMEP2.PrintSheets.csproj') -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$buildDll = Join-Path $root 'build\TungMEP2.PrintSheets.dll'
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($buildDll).ProductVersion -ne $version) { throw 'Wrong build version.' }
$package = Join-Path $root 'release\TungMEP2-PrintSheets'
$packageBin = Join-Path $package 'net48'
[IO.Directory]::CreateDirectory($packageBin) | Out-Null
Copy-Item -LiteralPath $buildDll -Destination $packageBin -Force
Get-ChildItem -LiteralPath (Join-Path $root 'installer') -File | Where-Object Extension -ne '.iss' | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $package -Force
}
$frameworkShell = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
& $frameworkShell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\Test-Maintenance.ps1') -Engine $buildDll
if ($LASTEXITCODE -ne 0) { throw 'Maintenance tests failed.' }
& $frameworkShell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\Test-Contracts.ps1') -PackageBin $packageBin -EngineOnly
if ($LASTEXITCODE -ne 0) { throw 'STT/UI contract tests failed.' }
$dist = Join-Path $root 'dist'
[IO.Directory]::CreateDirectory($dist) | Out-Null
$zip = Join-Path $dist "TungMEP2-PrintSheets-Portable-$version-Revit2024.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
$innoCompiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$buildId = (Get-FileHash -LiteralPath $buildDll -Algorithm SHA256).Hash.Substring(0,12)
& $innoCompiler /Qp "/DMyBuildId=$buildId" (Join-Path $root 'installer\TungMEP2-PrintSheets.iss')
if ($LASTEXITCODE -ne 0) { throw 'EXE packaging failed.' }
Write-Host "Verified maintenance package: $package"
Write-Host "Portable ZIP: $zip"
