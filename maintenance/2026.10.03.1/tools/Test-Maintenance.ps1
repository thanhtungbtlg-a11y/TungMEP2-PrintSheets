param([string]$Engine = (Join-Path $PSScriptRoot '..\build\TungMEP2.PrintSheets.dll'))
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[void][Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll')
$assembly = [Reflection.Assembly]::LoadFrom($Engine)
$helper = $assembly.GetType('VP_RevitAPI.Helper.Sheets.SheetExportUtilities', $true)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
$script:checks = 0
$script:failures = @()
function Assert($condition, $message) {
    $script:checks++
    if (-not $condition) { throw $message }
}
function Case($name, [scriptblock]$body) {
    try { & $body; Write-Host "PASS: $name" }
    catch { $script:failures += "$name : $($_.Exception.GetBaseException().Message)"; Write-Host "FAIL: $name : $($_.Exception.GetBaseException().Message)" }
}
function Invoke-Helper($name, [object[]]$values) {
    for ($index = 0; $index -lt $values.Length; $index++) { $values[$index] = $values[$index].PSObject.BaseObject }
    $helper.GetMethod($name, $flags).Invoke($null, $values)
}
Case 'Visible build and file metadata' {
    Assert ([Diagnostics.FileVersionInfo]::GetVersionInfo($Engine).FileVersion -eq '2026.10.3.1') 'Wrong FileVersion'
    Assert ($assembly.GetName().Version.ToString() -eq '2026.10.3.1') 'Wrong assembly version'
    $build = $assembly.GetType('VP_RevitAPI.Helper.Sheets.PrintSheetsBuild', $true)
    Assert ($build.GetField('Version').GetRawConstantValue() -eq '2026.10.03.1') 'Wrong visible version'
}
Case 'Long output no longer loses file-name budget' {
    $folder = 'C:\' + ('a' * 180) + '\' + ('b' * 100)
    Assert ((Invoke-Helper 'GetSafeBaseFileNameLength' @($folder, '.pdf')) -eq 160) 'Legacy 259-character cap remains'
}
Case 'Revit staging names stay within legacy API limits' {
    $staging = 'C:\' + ('a' * 180)
    $budget = Invoke-Helper 'GetLegacyStagingBaseNameLength' @($staging,'.dwg')
    Assert ($budget -eq 71) 'Revit staging budget not enforced'
    Assert (($staging.Length + 1 + $budget + 4) -le 259) 'Staged DWG path exceeds Revit limit'
    Assert (Invoke-Helper 'AreNativeBatchNamesSafe' @([string[]]@('A101 - Plan'),160)) 'Safe native batch rejected'
    Assert (-not (Invoke-Helper 'AreNativeBatchNamesSafe' @([string[]]@( ('X' * 180)),160))) 'Raw long sheet name not routed to single fallback'
    Assert (-not (Invoke-Helper 'AreNativeBatchNamesSafe' @([string[]]@('A101 - Plan'),5))) 'Long TEMP path does not bypass unsafe batch'
    Assert (-not (Invoke-Helper 'AreNativeBatchNamesSafe' @([string[]]@('A101 - Plan/Level'),160))) 'Invalid raw name not routed to single fallback'
}
Case 'Duplicate selection on adding/reordering sources' {
    $selection = $assembly.GetType('VP_RevitAPI.Helper.Sheets.PrintSheetSelection', $true)
    $restore = $selection.GetMethod('Restore')
    $ids = [long[]]@(10,20,10,30)
    $previous = [Nullable[bool][]]@($true,$true,$null,$null)
    $result = $restore.Invoke($null, [object[]]@($ids,$previous))
    Assert (($result -join ',') -eq 'True,True,False,True') 'Adding source checks duplicate'
    $result = $restore.Invoke($null,[object[]]@([long[]]@(10,10),[Nullable[bool][]]@($null,$false)))
    Assert (($result -join ',') -eq 'False,False') 'Explicitly unchecked sheet gets auto-selected'
    $result = $restore.Invoke($null,[object[]]@([long[]]@(10,20,10),[Nullable[bool][]]@($false,$true,$true)))
    Assert (($result -join ',') -eq 'False,True,True') 'User choice in later group lost'
    $state = [bool[]]@($true,$true,$false,$true)
    [void]$selection.GetMethod('SetSelection').Invoke($null,[object[]]@($ids,$state,2,$true))
    Assert (($state -join ',') -eq 'False,True,True,True') 'Checking later occurrence does not uncheck earlier'
    [void]$selection.GetMethod('SetSelection').Invoke($null,[object[]]@($ids,$state,2,$false))
    Assert (($state -join ',') -eq 'False,True,False,True') 'Uncheck modifies unrelated rows'
    $all = $selection.GetMethod('SelectAll').Invoke($null,[object[]]@($ids,$true))
    Assert (($all -join ',') -eq 'True,True,False,True') 'Select-all selects duplicates'
    $none = $selection.GetMethod('SelectAll').Invoke($null,[object[]]@($ids,$false))
    Assert (($none -join ',') -eq 'False,False,False,False') 'Deselect-all leaves selections'
    $key = [Func[long,long]] { param($id) $id }
    $distinct = $selection.GetMethod('DistinctInOrder').MakeGenericMethod([Type[]]@([long]))
    $unique = $distinct.Invoke($null,[object[]]@([long[]]@(30,10,30,20,10),$key))
    Assert (($unique -join ',') -eq '30,10,20') 'Export dedupe changes displayed order'
}
Case 'Real long-path file transfer, overwrite, locking and cleanup' {
    $testRoot = Join-Path ([IO.Path]::GetTempPath()) ('tm2-long-test-' + [Guid]::NewGuid().ToString('N'))
    $testRoot = [IO.Path]::GetFullPath($testRoot)
    if (-not $testRoot.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture root' }
    [void][IO.Directory]::CreateDirectory($testRoot)
    try {
        $folder = $testRoot + '\' + ('a' * 100) + '\' + ('b' * 100) + '\' + ('c' * 100)
        $prepared = Invoke-Helper 'PrepareOutputFolder' @($folder)
        Assert ($prepared -eq $folder) 'Output path changed'
        $files = $assembly.GetType('VP_RevitAPI.Helper.Sheets.LongPathFiles', $true)
        $native = $files.GetMethod('ToNativePath').Invoke($null,[object[]]@($folder))
        Assert ($native.StartsWith('\\?\C:\')) 'Local native prefix missing'
        $unc = $files.GetMethod('ToNativePath').Invoke($null,[object[]]@('\\server\share\one\..\two'))
        Assert ($unc -eq '\\?\UNC\server\share\two') 'UNC normalization incorrect'
        $target = $folder + '\' + ([char]0x0110) + ' test.pdf'
        $source = Join-Path $testRoot 'staged.pdf'
        [IO.File]::WriteAllText($source, "%PDF-1.4`nfirst`n%%EOF", [Text.Encoding]::ASCII)
        [void](Invoke-Helper 'ReplaceFileFromStaging' @($source,$target))
        Assert (Invoke-Helper 'IsCompletePdfFile' @($target)) 'Long final PDF not verified'
        $read = $files.GetMethod('OpenRead')
        $stream = $read.Invoke($null,[object[]]@($target))
        try { Assert ($stream.Length -eq (Get-Item -LiteralPath $source).Length) 'Wrong copied length' } finally { $stream.Dispose() }
        [IO.File]::WriteAllText($source, "%PDF-1.4`nsecond-longer-content`n%%EOF", [Text.Encoding]::ASCII)
        [void](Invoke-Helper 'ReplaceFileFromStaging' @($source,$target))
        Assert (Invoke-Helper 'IsCompletePdfFile' @($target)) 'Overwritten final PDF invalid'
        $locked = $read.Invoke($null,[object[]]@($target))
        try {
            $failed = $false
            try { [void](Invoke-Helper 'ReplaceFileFromStaging' @($source,$target)) } catch { $failed = $true }
            Assert $failed 'Locked output was replaced'
        } finally { $locked.Dispose() }
        $missingFailed = $false
        try { [void](Invoke-Helper 'ReplaceFileFromStaging' @((Join-Path $testRoot 'missing.pdf'),$target)) } catch { $missingFailed = $true }
        Assert $missingFailed 'Missing source accepted'
        Assert (Invoke-Helper 'IsCompletePdfFile' @($target)) 'Previous final output lost after failure'
        $entries = @($files.GetMethod('EnumerateFiles').Invoke($null,[object[]]@($folder,'*')))
        Assert ($entries.Count -eq 1) 'Pending/backup/probe files leaked'
        Assert ([IO.File]::Exists($source)) 'Staging source removed before caller cleanup'
        $legacyTarget = $folder + '\Project - Sheet - A101 - Plan.pdf'
        [void](Invoke-Helper 'ReplaceFileFromStaging' @($source,$legacyTarget))
        Assert ((Invoke-Helper 'CountPrintedPdfFilesNeedingRename' @($folder)) -eq 1) 'Long-path rename discovery failed'
        $renamed = Invoke-Helper 'RenamePrintedPdfFiles' @($folder)
        Assert ($renamed.RenamedCount -eq 1) 'Long-path rename failed'
        $renamedTarget = $folder + '\A101 - Plan.pdf'
        Assert (Invoke-Helper 'IsCompletePdfFile' @($renamedTarget)) 'Renamed PDF was lost'
        $dwgFolder = Join-Path $testRoot 'dwg-stage'
        [void][IO.Directory]::CreateDirectory($dwgFolder)
        [IO.File]::WriteAllText((Join-Path $dwgFolder 'sample.dwg'), 'dwg-test')
        [IO.File]::WriteAllText((Join-Path $dwgFolder 'sample.pcp'), 'sidecar-test')
        [void](Invoke-Helper 'CopyDwgStagingArtifacts' @($dwgFolder,$folder,'sample.dwg'))
        Assert ($files.GetMethod('Length').Invoke($null,[object[]]@($folder + '\sample.dwg')) -eq 8) 'Long-path DWG transfer failed'
        Assert ($files.GetMethod('Length').Invoke($null,[object[]]@($folder + '\sample.pcp')) -eq 12) 'DWG sidecar not copied'
        $files.GetMethod('Delete').Invoke($null,[object[]]@($target)) | Out-Null
    } finally {
        # Exact GUID fixture, bounded to Temp; modern host supports its long descendants.
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
Write-Host "Assertions: $script:checks; failures: $($script:failures.Count)"
if ($script:failures.Count) { throw ($script:failures -join "`n") }
