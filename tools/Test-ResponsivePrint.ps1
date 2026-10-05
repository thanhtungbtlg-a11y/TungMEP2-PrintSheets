[CmdletBinding()]
param([switch]$StateOnly)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$packageBin = Join-Path $root 'release\TungMEP2-PrintSheets\net48'
$assemblyPath = Join-Path $packageBin 'TungMEP2.PrintSheets.dll'
$revitDirectory = 'C:\Program Files\Autodesk\Revit 2024'

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
    [void][Reflection.Assembly]::LoadFrom((Join-Path $revitDirectory 'RevitAPI.dll'))
    $assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)

    if (-not ('TestNativeWrapper' -as [type])) {
        Add-Type -TypeDefinition @'
using System;

public sealed class TestNativeWrapper : IEquatable<TestNativeWrapper>
{
    private readonly int _nativeId;

    public TestNativeWrapper(int nativeId) { _nativeId = nativeId; }
    public bool Equals(TestNativeWrapper other) =>
        other != null && other._nativeId == _nativeId;
    public override bool Equals(object value) => Equals(value as TestNativeWrapper);
    public override int GetHashCode() => _nativeId;
}
'@
    }
    $identityType = $assembly.GetType(
        'TungMEP2.PrintSheets.Execution.NativeObjectIdentity', $true)
    $isSameMethod = $identityType.GetMethod('IsSame').MakeGenericMethod(
        [TestNativeWrapper])
    $capturedWrapper = [TestNativeWrapper]::new(42)
    $activeWrapper = [TestNativeWrapper]::new(42)
    Assert-True (-not [object]::ReferenceEquals($capturedWrapper, $activeWrapper)) `
        'test uses distinct managed wrappers'
    Assert-True ([bool]$isSameMethod.Invoke(
        $null, @($capturedWrapper, $activeWrapper))) `
        'equal native identity is accepted across managed wrappers'
    Assert-True (-not [bool]$isSameMethod.Invoke(
        $null, @([TestNativeWrapper]::new(42), [TestNativeWrapper]::new(99)))) `
        'different native identities are rejected'

    $machineType = $assembly.GetType(
        'TungMEP2.PrintSheets.Execution.PrintJobStateMachine', $true)
    $machine = [Activator]::CreateInstance($machineType)
    Assert-True ($machine.State.ToString() -eq 'Ready') 'job starts Ready'
    $machine.Start(3)
    Assert-True ($machine.State.ToString() -eq 'WaitingForRevit') `
        'started job waits for Revit'
    Assert-True ([bool]$machine.TryBeginNext()) 'first unit can start'
    Assert-True ($machine.State.ToString() -eq 'Exporting') `
        'active unit is Exporting'
    $machine.CompleteCurrent($true)
    Assert-True ($machine.CompletedCount -eq 1) 'completion is counted once'
    $machine.Pause()
    Assert-True ($machine.State.ToString() -eq 'Paused') 'pause is visible'
    Assert-True (-not [bool]$machine.TryBeginNext()) `
        'paused job cannot start another unit'
    $machine.Resume()
    $machine.RequestStop()
    Assert-True ($machine.State.ToString() -eq 'Stopped') 'stop is terminal'
    Assert-True (-not [bool]$machine.TryBeginNext()) `
        'stopped job cannot start another unit'
    Assert-True ($machine.PendingCount -eq 2) 'stopped job retains pending count'

    if ($StateOnly) {
        Write-Host 'Responsive PrintSheets state test passed.' -ForegroundColor Green
        return
    }

    $executionFolder = Join-Path $root 'src\Core\Execution'
    foreach ($fileName in @(
        'PrintSheetsWindowHost.cs',
        'RevitUiActionBridge.cs',
        'ResponsivePrintExternalEventHandler.cs',
        'ResponsivePrintScheduler.cs',
        'RevitPrintWorkExecutor.cs',
        'VirtualPdfOutputMonitor.cs')) {
        Assert-True (Test-Path -LiteralPath (Join-Path $executionFolder $fileName)) `
            "responsive implementation contains $fileName"
    }
    $schedulerSource = Get-Content -Raw (Join-Path $executionFolder 'ResponsivePrintScheduler.cs')
    Assert-True ($schedulerSource.Contains('CooperativeDelayMilliseconds = 750')) `
        'cooperative delay is 750 ms'
    $handlerSource = Get-Content -Raw (Join-Path $executionFolder 'ResponsivePrintExternalEventHandler.cs')
    Assert-True ($handlerSource.Contains('ExecuteOne(app)')) `
        'external event delegates one work unit'
    Assert-True (Test-Path -LiteralPath (Join-Path $root 'src\Core\Pdf\PdfMergeService.cs')) `
        'responsive implementation contains PdfMergeService'

    $commandSource = Get-Content -Raw (Join-Path $root 'src\Command_PrintSheets.cs')
    $windowSource = Get-Content -Raw (Join-Path $root 'src\Core\UI\UI_PrintSheetsWindow.cs')
    $utilitySource = Get-Content -Raw (Join-Path $root 'src\Core\Helper\Sheets\SheetExportUtilities.cs')
    Assert-True (-not $commandSource.Contains('.ShowDialog(')) `
        'command no longer opens a modal window'
    Assert-True ($commandSource.Contains('PrintSheetsWindowHost.ShowOrActivate')) `
        'command delegates modeless lifetime to the host'
    Assert-True (-not $windowSource.Contains('Dispatcher.PushFrame')) `
        'responsive execution does not pump a nested dispatcher frame'
    Assert-True (-not $windowSource.Contains('Thread.Sleep')) `
        'modeless window never blocks its dispatcher with Sleep'
    Assert-True ($utilitySource.Contains('SubmitVirtualPdf')) `
        'virtual-printer Revit submission is exposed separately'
    Assert-True ($utilitySource.Contains('CompleteVirtualPdfOutput')) `
        'virtual-printer file completion is exposed separately'
    Write-Host 'Responsive PrintSheets test passed.' -ForegroundColor Green
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}

& (Join-Path $PSScriptRoot 'Test-PdfMerge.ps1')
& (Join-Path $PSScriptRoot 'Test-SingleFormatOutputPlan.ps1')
