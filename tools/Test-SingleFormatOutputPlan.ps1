[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$packageBin = Join-Path $root 'release\TungMEP2-PrintSheets\net48'
$assemblyPath = Join-Path $packageBin 'TungMEP2.PrintSheets.dll'
$revitDirectory = 'C:\Program Files\Autodesk\Revit 2024'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'TungMEP2-SingleFormatPlan-' + [Guid]::NewGuid().ToString('N'))

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $fileName = ([Reflection.AssemblyName]::new($eventArgs.Name)).Name + '.dll'
    foreach ($directory in @($packageBin, $revitDirectory)) {
        $candidate = Join-Path $directory $fileName
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [Reflection.Assembly]::LoadFrom($candidate)
        }
    }
    return $null
}

[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [IO.Directory]::CreateDirectory($testRoot) | Out-Null
    [void][Reflection.Assembly]::LoadFrom((Join-Path $revitDirectory 'RevitAPI.dll'))
    $assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
    $sourceType = $assembly.GetType(
        'TungMEP2.PrintSheets.Execution.PrintWorkSource', $true)
    $builderType = $assembly.GetType(
        'TungMEP2.PrintSheets.Execution.PrintWorkPlanBuilder', $true)
    $build = $builderType.GetMethod('Build', [Reflection.BindingFlags]'Public,Static')
    Assert-True ($null -ne $build) 'PrintWorkPlanBuilder.Build must exist'

    $sources = [Array]::CreateInstance($sourceType, 1)
    $sources.SetValue(
        [Activator]::CreateInstance($sourceType, [object[]]@([long]101, [string]'N-HS-A.100')), 0)
    $pdfFolder = Join-Path $testRoot 'pdf'
    $dwgFolder = Join-Path $testRoot 'dwg'
    [IO.Directory]::CreateDirectory($pdfFolder) | Out-Null
    [IO.Directory]::CreateDirectory($dwgFolder) | Out-Null

    $pdfOnly = @($build.Invoke($null, [object[]]@(
        $sources, [bool]$true, [string]$pdfFolder,
        [bool]$false, [string]'', [bool]$false, [string]'')))
    Assert-True ($pdfOnly.Count -eq 1) 'PDF-only plan must contain one item'
    Assert-True ($pdfOnly[0].Kind.ToString() -eq 'Pdf') `
        'PDF-only plan must not touch or add the disabled DWG format'

    $dwgOnly = @($build.Invoke($null, [object[]]@(
        $sources, [bool]$false, [string]'',
        [bool]$true, [string]$dwgFolder, [bool]$false, [string]'')))
    Assert-True ($dwgOnly.Count -eq 1) 'DWG-only plan must contain one item'
    Assert-True ($dwgOnly[0].Kind.ToString() -eq 'Dwg') `
        'DWG-only plan must not touch or add the disabled PDF format'

    Write-Host 'Single-format output plan regression passed.' -ForegroundColor Green
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
