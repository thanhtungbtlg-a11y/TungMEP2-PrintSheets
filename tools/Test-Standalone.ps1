[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$packageBin = Join-Path $root 'release\TungMEP2-PrintSheets\net48'
$assemblyPath = Join-Path $packageBin 'TungMEP2.PrintSheets.dll'
$revitDirectory = 'C:\Program Files\Autodesk\Revit 2024'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build output not found: $assemblyPath"
}

$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)

    $assemblyFileName = [Reflection.AssemblyName]::new(
        $eventArgs.Name).Name + '.dll'
    foreach ($directory in @($packageBin, $revitDirectory)) {
        $candidate = Join-Path $directory $assemblyFileName
        if (Test-Path -LiteralPath $candidate) {
            return [Reflection.Assembly]::LoadFrom($candidate)
        }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
$testRoot = Join-Path (
    [IO.Path]::GetTempPath()) (
        'TungMEP2-StandalonePrintSheets-' +
        [Guid]::NewGuid().ToString('N'))

try {
    [Reflection.Assembly]::LoadFrom(
        (Join-Path $revitDirectory 'RevitAPI.dll')) | Out-Null
    $assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)

    Assert-True ($assembly.GetName().Name -eq 'TungMEP2.PrintSheets') `
        'standalone assembly identity must be unique'
    $assemblyText = [Text.Encoding]::ASCII.GetString(
        [IO.File]::ReadAllBytes($assemblyPath))
    Assert-True ($assemblyText.Contains('TungMEP2.PrintSheets')) `
        'standalone namespace must be compiled'
    Assert-True ($assemblyText.Contains('Command_PrintSheets')) `
        'standalone command entry point must be compiled'
    $manifestResourceNames = @($assembly.GetManifestResourceNames())
    Assert-True (
        $manifestResourceNames -contains
        'TungMEP2.PrintSheets.Resources.Printer_16.png') `
        '16px Ribbon icon must be embedded directly in the DLL'
    Assert-True (
        $manifestResourceNames -contains
        'TungMEP2.PrintSheets.Resources.Printer_32.png') `
        '32px Ribbon icon must be embedded directly in the DLL'

    $references = @($assembly.GetReferencedAssemblies() | ForEach-Object Name)
    foreach ($forbiddenReference in @(
        'TungMEP2',
        'VP_RevitAPI',
        'Nice3point.Revit.Toolkit',
        'Nice3point.Revit.Extensions',
        'System.Text.Json')) {
        Assert-True ($references -notcontains $forbiddenReference) `
            "standalone assembly must not reference $forbiddenReference"
    }

    $sheetUtilitiesType = $assembly.GetType(
        'VP_RevitAPI.Helper.SheetUtilities',
        $true)
    Assert-True (
        $null -ne $sheetUtilitiesType.GetMethod('SetSttValues')) `
        'standalone PrintSheets must expose explicit STT write from the sheet list'
    Assert-True (
        $null -ne $sheetUtilitiesType.GetMethod('EnsureSttForExport')) `
        'standalone PrintSheets must include automatic STT repair'
    $requiresSttRepairMethod = $sheetUtilitiesType.GetMethod(
        'RequiresAutomaticSttRepair',
        [Reflection.BindingFlags]'NonPublic,Static')
    Assert-True ($null -ne $requiresSttRepairMethod) `
        'standalone PrintSheets must validate contiguous STT'
    $validSttArguments = [object[]]::new(2)
    $validSttArguments[0] = [string[]]@('1', '2', '3')
    $validSttArguments[1] = [int]1
    $gappedSttArguments = [object[]]::new(2)
    $gappedSttArguments[0] = [string[]]@('1', '3', '5')
    $gappedSttArguments[1] = [int]1
    $wrongOrderSttArguments = [object[]]::new(2)
    $wrongOrderSttArguments[0] = [string[]]@('3', '1', '2')
    $wrongOrderSttArguments[1] = [int]1
    Assert-True (
        -not [bool]$requiresSttRepairMethod.Invoke(
            $null,
            $validSttArguments)) `
        'standalone PrintSheets must preserve STT 1..n'
    Assert-True (
        [bool]$requiresSttRepairMethod.Invoke(
            $null,
            $gappedSttArguments)) `
        'standalone PrintSheets must repair gapped STT'
    Assert-True (
        [bool]$requiresSttRepairMethod.Invoke(
            $null,
            $wrongOrderSttArguments)) `
        'standalone PrintSheets must repair STT in the wrong Sheet Number order'

    $utilitiesType = $assembly.GetType(
        'VP_RevitAPI.Helper.Sheets.SheetExportUtilities',
        $true)
    Assert-True (
        $null -ne $utilitiesType.GetMethod('ListAvailableSheetListSchedules')) `
        'standalone tool must list Sheet List schedules as sources'
    Assert-True (
        $null -ne $utilitiesType.GetMethod('ResolveSheetsFromSheetListSchedule')) `
        'standalone tool must resolve filtered sheets from a Sheet List schedule'
    Assert-True (
        $null -ne $utilitiesType.GetMethod('ListAvailablePrinters')) `
        'standalone tool must list Native PDF and virtual PDF printers'
    Assert-True (
        $null -ne $utilitiesType.GetMethod('ListAvailablePrintSetups')) `
        'standalone tool must list Revit Print Setups for virtual printers'
    Assert-True (
        $null -ne $utilitiesType.GetMethod('NormalizePdfFileBaseNameForFolder')) `
        'responsive combine must retain long-path-safe PDF naming'
    foreach ($fieldCheck in @{
        DwgStagingRootName = 'TungMEP2-DwgExport'
        PdfStagingRootName = 'TungMEP2-PdfExport'
        VirtualPdfStagingRootName = 'TungMEP2-VirtualPdfPrint'
    }.GetEnumerator()) {
        $field = $utilitiesType.GetField(
            $fieldCheck.Key,
            [Reflection.BindingFlags]'NonPublic,Static')
        Assert-True (
            [string]$field.GetRawConstantValue() -eq [string]$fieldCheck.Value) `
            "missing local staging constant $($fieldCheck.Key)"
    }
    foreach ($fieldCheck in @{
        VirtualPrinterOutputTimeoutMilliseconds = 60000
        VirtualPrinterCombinedOutputTimeoutMilliseconds = 300000
        VirtualPrinterFileStabilityMilliseconds = 3000
    }.GetEnumerator()) {
        $field = $utilitiesType.GetField(
            $fieldCheck.Key,
            [Reflection.BindingFlags]'NonPublic,Static')
        Assert-True (
            [int]$field.GetRawConstantValue() -eq [int]$fieldCheck.Value) `
            "standalone virtual PDF wait setting $($fieldCheck.Key) is not synchronized"
    }
    foreach ($methodName in @(
        'ExportSheetsToVirtualPdf',
        'PrintSheetsWithVirtualPrinter',
        'IsLikelyPdfVirtualPrinter',
        'IsCompletePdfFile',
        'ReplaceFileFromStaging')) {
        $method = $utilitiesType.GetMethod(
            $methodName,
            [Reflection.BindingFlags]'NonPublic,Static')
        Assert-True ($null -ne $method) `
            "standalone tool must contain $methodName"
    }
    $isCompletePdfMethod = $utilitiesType.GetMethod(
        'IsCompletePdfFile',
        [Reflection.BindingFlags]'NonPublic,Static')
    $waitForVirtualPrinterPdfMethod = $utilitiesType.GetMethod(
        'WaitForVirtualPrinterPdf',
        [Reflection.BindingFlags]'NonPublic,Static')
    foreach ($methodName in @(
        'ConfigurePdfXChangeLiteAutoSave',
        'FindPdfXChangeLitePdfSaverExecutable',
        'DumpPdfXChangeLiteOptions',
        'SetPdfXChangeLiteSaveOptions',
        'TryParsePdfXChangeBooleanOption')) {
        $method = $utilitiesType.GetMethod(
            $methodName,
            [Reflection.BindingFlags]'NonPublic,Static')
        Assert-True ($null -ne $method) `
            "standalone PDF-XChange Lite integration must contain $methodName"
    }
    $parsePdfXChangeBooleanMethod = $utilitiesType.GetMethod(
        'TryParsePdfXChangeBooleanOption',
        [Reflection.BindingFlags]'NonPublic,Static')
    $parsePdfXChangeArguments = [object[]]::new(3)
    $parsePdfXChangeArguments[0] = [string[]]@(
        'Save.ShowSaveDialog=1; Yes',
        'Save.RunApp=0; No')
    $parsePdfXChangeArguments[1] = [string]'Save.ShowSaveDialog'
    $parsePdfXChangeArguments[2] = [bool]$false
    Assert-True (
        [bool]$parsePdfXChangeBooleanMethod.Invoke(
            $null,
            $parsePdfXChangeArguments)) `
        'standalone tool must parse PDF-XChange Lite Save options'
    Assert-True ([bool]$parsePdfXChangeArguments[2]) `
        'standalone tool must read enabled PDF-XChange Lite options'
    [IO.Directory]::CreateDirectory($testRoot) | Out-Null
    $completePdfPath = Join-Path $testRoot 'complete.pdf'
    $incompletePdfPath = Join-Path $testRoot 'incomplete.pdf'
    [IO.File]::WriteAllBytes(
        $completePdfPath,
        [Text.Encoding]::ASCII.GetBytes("%PDF-1.7 probe`n%%EOF`n"))
    [IO.File]::WriteAllBytes(
        $incompletePdfPath,
        [Text.Encoding]::ASCII.GetBytes('%PDF-1.7 still-writing'))
    Assert-True (
        [bool]$isCompletePdfMethod.Invoke(
            $null,
            [object[]]@([string]$completePdfPath))) `
        'standalone tool must accept a closed PDF with %%EOF'
    Assert-True (
        -not [bool]$isCompletePdfMethod.Invoke(
            $null,
            [object[]]@([string]$incompletePdfPath))) `
        'standalone tool must reject PDF output before %%EOF is written'
    $waitArguments = [object[]]::new(5)
    $waitArguments[0] = [string]$testRoot
    $waitArguments[1] = [string]$completePdfPath
    $waitArguments[2] = [int]2000
    $waitArguments[3] = [int]300
    $waitArguments[4] = $null
    $waitStopwatch = [Diagnostics.Stopwatch]::StartNew()
    $detectedPdfPath = [string]$waitForVirtualPrinterPdfMethod.Invoke(
        $null,
        $waitArguments)
    $waitStopwatch.Stop()
    Assert-True ($detectedPdfPath -eq $completePdfPath) `
        'standalone tool must detect the stable completed PDF'
    Assert-True ($waitStopwatch.ElapsedMilliseconds -ge 250) `
        'standalone tool must wait for file stability before reporting completion'

    $printerOptionType = $assembly.GetType(
        'VP_RevitAPI.Helper.Sheets.PrinterOption',
        $true)
    $nativePrinter = $printerOptionType.GetMethod(
        'CreateNativePdf',
        [Reflection.BindingFlags]'Public,Static').Invoke($null, @())
    Assert-True ($nativePrinter.IsNativePdf) `
        'Revit Native PDF must remain the default PDF engine'
    Assert-True ([string]::IsNullOrWhiteSpace($nativePrinter.DriverName)) `
        'Native PDF must not depend on a Windows printer driver'
    $createVirtualPrinterMethod = $printerOptionType.GetMethod(
        'CreateVirtualPrinter',
        [Reflection.BindingFlags]'Public,Static')
    $pdfXChangeLite = $createVirtualPrinterMethod.Invoke(
        $null,
        [object[]]@([string]'PDF-XChange Lite', [bool]$false))
    $foxit = $createVirtualPrinterMethod.Invoke(
        $null,
        [object[]]@([string]'Foxit PDF Printer', [bool]$true))
    $printerOptions = [Array]::CreateInstance($printerOptionType, 3)
    $printerOptions.SetValue($nativePrinter, 0)
    $printerOptions.SetValue($foxit, 1)
    $printerOptions.SetValue($pdfXChangeLite, 2)
    $defaultPrinterArguments = [object[]]::new(1)
    $defaultPrinterArguments[0] = $printerOptions
    $defaultPrinter = $utilitiesType.GetMethod(
        'SelectDefaultPdfPrinter').Invoke(
            $null,
            $defaultPrinterArguments)
    Assert-True (
        $defaultPrinter.DriverName -eq 'PDF-XChange Lite') `
        'standalone tool must prefer PDF-XChange Lite by default'
    $fallbackOptions = [Array]::CreateInstance($printerOptionType, 2)
    $fallbackOptions.SetValue($nativePrinter, 0)
    $fallbackOptions.SetValue($foxit, 1)
    $fallbackArguments = [object[]]::new(1)
    $fallbackArguments[0] = $fallbackOptions
    $fallbackPrinter = $utilitiesType.GetMethod(
        'SelectDefaultPdfPrinter').Invoke(
            $null,
            $fallbackArguments)
    Assert-True ($fallbackPrinter.IsNativePdf) `
        'standalone tool must fallback to Native PDF without PDF-XChange Lite'

    $windowType = $assembly.GetType('VP_RevitAPI.UI.UI_PrintSheetsWindow', $true)
    $sheetSourceOptionType = $assembly.GetType(
        'VP_RevitAPI.UI.UI_PrintSheetsWindow+SheetSourceOption',
        $true)
    Assert-True ($null -ne $sheetSourceOptionType) `
        'redesigned UI must expose a unified Print Set/Schedule source option'
    foreach ($fieldName in @(
        'OutputFolderTextBox',
        'DwgOutputFolderTextBox',
        'ExportPdfCheckBox',
        'ExportDwgCheckBox',
        'SheetsDataGrid',
        'SheetSourcesListBox',
        'PrinterComboBox',
        'PdfExportSetupComboBox',
        'PdfOutputProcessingComboBox',
        'SttStartTextBox',
        'SttStatusTextBlock',
        'ToggleSettingsButton',
        'SourceControlsPanel',
        'ExportSettingsPanel',
        'SheetListControlsPanel',
        'SttControlsPanel',
        'ExportButton',
        'CloseButton')) {
        $field = $windowType.GetField(
            $fieldName,
            [Reflection.BindingFlags]'NonPublic,Instance')
        Assert-True ($null -ne $field) "redesigned UI field $fieldName must exist"
    }
    foreach ($methodName in @(
        'SuppressSelectionForPdfExport',
        'RestoreSelectionAfterExport',
        'OnSheetSourcesSelectionChanged',
        'OnSheetSourceCheckedChanged',
        'OnSelectAllSourcesClick',
        'OnSelectNoSourcesClick',
        'OnMoveSourceUpClick',
        'OnMoveSourceDownClick',
        'OnToggleSettingsClick',
        'OnMoveSheetUpClick',
        'OnMoveSheetDownClick',
        'OnGenerateSttClick',
        'OnApplySttClick',
        'InitializePdfProcessingList',
        'OnPrinterSelectionChanged',
        'RefreshPdfSetupOptionsForSelectedEngine')) {
        $method = $windowType.GetMethod(
            $methodName,
            [Reflection.BindingFlags]'NonPublic,Instance')
        Assert-True ($null -ne $method) `
            "standalone PrintSheets window must contain $methodName"
    }
    foreach ($dimension in @{
        DefaultWindowWidth = 1180
        DefaultWindowHeight = 960
        MinimumWindowWidth = 960
        MinimumWindowHeight = 720
    }.GetEnumerator()) {
        $field = $windowType.GetField(
            $dimension.Key,
            [Reflection.BindingFlags]'NonPublic,Static')
        Assert-True (
            [double]$field.GetRawConstantValue() -eq
            [double]$dimension.Value) `
            "standalone window dimension $($dimension.Key) is not synchronized"
    }
    $uiSource = Get-Content -LiteralPath (
        Join-Path $root 'src\Core\UI\UI_PrintSheetsWindow.cs') -Raw
    Assert-True (
        -not $uiSource.Contains('SheetUtilities.EnsureSttForExport')) `
        'standalone export flow must not silently rewrite STT'
    Assert-True (
        $uiSource.Contains('SheetUtilities.SetSttValues')) `
        'standalone sheet list must explicitly write user-approved STT values'

    $stateType = $assembly.GetType(
        'VP_RevitAPI.State.PrintSheetsRecentOutputFolderStore',
        $true)
    $storageMethod = $stateType.GetMethod(
        'GetStorageDirectoryPath',
        [Reflection.BindingFlags]'NonPublic,Static')
    $storagePath = [string]$storageMethod.Invoke($null, @())
    Assert-True ($storagePath -like '*\TungMEP2-PrintSheets\State') `
        'standalone state must use its own storage folder'
    Assert-True ($storagePath -notlike '*\TungMEP2\State\*') `
        'standalone state must not use the full TungMEP2 state folder'

    $generatedResources = $assembly.GetManifestResourceStream(
        'TungMEP2.PrintSheets.g.resources')
    Assert-True ($null -ne $generatedResources) 'WPF resources must be packaged'
    try {
        $reader = [Resources.ResourceReader]::new($generatedResources)
        try {
            $found = $false
            $enumerator = $reader.GetEnumerator()
            while ($enumerator.MoveNext()) {
                if ([string]$enumerator.Key -ieq 'core/ui/ui_printsheetswindow.baml') {
                    $found = $true
                    break
                }
            }
            Assert-True $found 'PrintSheets XAML must be packaged'
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $generatedResources.Dispose()
    }

    $dependencyNames = @(
        'PdfSharpCore.dll',
        'SixLabors.Fonts.dll',
        'SixLabors.ImageSharp.dll',
        'ICSharpCode.SharpZipLib.dll')
    foreach ($dependencyName in $dependencyNames) {
        Assert-True (Test-Path -LiteralPath (Join-Path $packageBin $dependencyName)) `
            "standalone package must contain $dependencyName"
    }
    foreach ($forbiddenPayload in @(
        'RevitAPI.dll',
        'RevitAPIUI.dll',
        'TungMEP2.dll',
        'VP_RevitAPI.dll')) {
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $packageBin $forbiddenPayload))) `
            "standalone package must exclude $forbiddenPayload"
    }
    $portableZip = Join-Path $root 'dist\TungMEP2-PrintSheets-Portable-Revit2024.zip'
    $setupExe = Join-Path $root 'dist\TungMEP2-PrintSheets-Setup-Revit2024.exe'
    Assert-True (Test-Path -LiteralPath $setupExe) 'Revit 2024 setup EXE must exist'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($portableZip)
    try {
        $entryNames = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($dependencyName in $dependencyNames) {
            Assert-True ($entryNames -contains "net48/$dependencyName") `
                "portable ZIP must contain $dependencyName"
        }
        Assert-True ($entryNames -contains 'THIRD-PARTY-NOTICES.txt') `
            'portable ZIP must contain third-party notices'
    }
    finally {
        $zip.Dispose()
    }

    Write-Host 'Standalone PrintSheets test passed.' -ForegroundColor Green
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
        $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if ($resolvedTestRoot.StartsWith(
                $resolvedTempRoot,
                [StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $resolvedTestRoot).StartsWith(
                'TungMEP2-StandalonePrintSheets-',
                [StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
        }
    }
}

& (Join-Path $PSScriptRoot 'Test-ResponsivePrint.ps1')
