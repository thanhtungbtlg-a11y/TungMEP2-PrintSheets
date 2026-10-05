using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace TungMEP2.PrintSheets.Execution;

public sealed class PrintJobRequest
{
	public Document SourceDocument { get; }
	public string DocumentKey { get; }
	public IReadOnlyList<PrintWorkItem> Items { get; }
	public string PdfOutputFolder { get; }
	public string DwgOutputFolder { get; }
	public PDFExportQualityType PdfQuality { get; }
	public bool AlwaysUseRaster { get; }
	public string VirtualPrinterName { get; }
	public long PrintSetupIdValue { get; }
	public long DwgExportSetupIdValue { get; }
	public bool CombinePdf { get; }
	public string CombinedPdfPath { get; }
	public string JobTemporaryFolder { get; }

	public PrintJobRequest(
		Document sourceDocument,
		IReadOnlyList<PrintWorkItem> items,
		string pdfOutputFolder,
		string dwgOutputFolder,
		PDFExportQualityType pdfQuality,
		bool alwaysUseRaster,
		string virtualPrinterName,
		long printSetupIdValue,
		long dwgExportSetupIdValue,
		bool combinePdf,
		string combinedPdfPath,
		string jobTemporaryFolder)
	{
		SourceDocument = sourceDocument ?? throw new ArgumentNullException(nameof(sourceDocument));
		Items = items ?? throw new ArgumentNullException(nameof(items));
		DocumentKey = sourceDocument.GetHashCode().ToString() + ":" + sourceDocument.Title;
		PdfOutputFolder = pdfOutputFolder ?? string.Empty;
		DwgOutputFolder = dwgOutputFolder ?? string.Empty;
		PdfQuality = pdfQuality;
		AlwaysUseRaster = alwaysUseRaster;
		VirtualPrinterName = virtualPrinterName ?? string.Empty;
		PrintSetupIdValue = printSetupIdValue;
		DwgExportSetupIdValue = dwgExportSetupIdValue;
		CombinePdf = combinePdf;
		CombinedPdfPath = combinedPdfPath ?? string.Empty;
		JobTemporaryFolder = jobTemporaryFolder ?? string.Empty;
	}
}
