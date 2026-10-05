$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$packageBin = Join-Path $root 'release\TungMEP2-PrintSheets\net48'

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "Assertion failed: $message" }
}

foreach ($name in @(
    'SixLabors.Fonts.dll',
    'SixLabors.ImageSharp.dll',
    'ICSharpCode.SharpZipLib.dll',
    'PdfSharpCore.dll',
    'TungMEP2.PrintSheets.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $packageBin $name))
}

$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $packageBin 'TungMEP2.PrintSheets.dll'))
$mergeType = $assembly.GetType('TungMEP2.PrintSheets.Pdf.PdfMergeService', $true)
$service = [Activator]::CreateInstance($mergeType)
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('TungMEP2-PdfMergeTest-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($sandbox) | Out-Null
try {
    $inputs = @()
    foreach ($width in @(111, 222)) {
        $path = Join-Path $sandbox ("$width.pdf")
        $document = New-Object PdfSharpCore.Pdf.PdfDocument
        $page = $document.AddPage()
        $page.Width = [PdfSharpCore.Drawing.XUnit]::FromPoint($width)
        $document.Save($path)
        $document.Dispose()
        $inputs += $path
    }

    $destination = Join-Path $sandbox 'combined.pdf'
    $result = $service.MergeAsync(
        [string[]]$inputs,
        $destination,
        [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Assert-True $result.Succeeded 'valid PDFs merge successfully'
    Assert-True ($result.PageCount -eq 2) 'merged page count is exact'
    $merged = [PdfSharpCore.Pdf.IO.PdfReader]::Open(
        $destination,
        [PdfSharpCore.Pdf.IO.PdfDocumentOpenMode]::Import)
    Assert-True ([math]::Round($merged.Pages[0].Width.Point) -eq 111) 'first input remains first'
    Assert-True ([math]::Round($merged.Pages[1].Width.Point) -eq 222) 'second input remains second'
    $merged.Dispose()

    [IO.File]::WriteAllText($destination, 'OLD_DESTINATION')
    $corrupt = Join-Path $sandbox 'corrupt.pdf'
    [IO.File]::WriteAllText($corrupt, 'not a pdf')
    $failed = $service.MergeAsync(
        [string[]]@($inputs[0], $corrupt),
        $destination,
        [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    Assert-True (-not $failed.Succeeded) 'corrupt input fails the merge'
    Assert-True ([IO.File]::ReadAllText($destination) -eq 'OLD_DESTINATION') `
        'failed merge preserves an existing destination'
    Write-Host 'PDF merge tests passed.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $sandbox) {
        Remove-Item -LiteralPath $sandbox -Recurse -Force
    }
}
