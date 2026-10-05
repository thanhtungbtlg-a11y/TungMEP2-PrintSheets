using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using VP_RevitAPI.Helper.Sheets;

namespace TungMEP2.PrintSheets.Execution;

public sealed class RevitPrintWorkExecutor
{
	public PrintWorkResult ExecuteOne(
		UIApplication application,
		PrintJobRequest request,
		PrintWorkItem item)
	{
		if (application == null) throw new ArgumentNullException(nameof(application));
		if (request == null) throw new ArgumentNullException(nameof(request));
		if (item == null) throw new ArgumentNullException(nameof(item));
		UIDocument active = application.ActiveUIDocument;
		if (active == null ||
			!NativeObjectIdentity.IsSame(request.SourceDocument, active.Document))
			return PrintWorkResult.Failure("SOURCE_DOCUMENT_NOT_ACTIVE");
		if (!request.SourceDocument.IsValidObject)
			return PrintWorkResult.Failure("SOURCE_DOCUMENT_CLOSED");
		if (!(request.SourceDocument.GetElement(new ElementId(item.SheetIdValue)) is ViewSheet sheet))
			return PrintWorkResult.Failure("SHEET_NOT_FOUND");
		if (!sheet.CanBePrinted)
			return PrintWorkResult.Failure($"Sheet {sheet.SheetNumber} cannot be printed.");

		try
		{
			if (item.Kind == PrintWorkKind.Pdf)
			{
				string folder = request.CombinePdf
					? request.JobTemporaryFolder
					: request.PdfOutputFolder;
				if (!string.IsNullOrWhiteSpace(request.VirtualPrinterName))
				{
					var submission = SheetExportUtilities.SubmitVirtualPdf(
						request.SourceDocument,
						sheet,
						folder,
						item.OutputBaseName,
						request.VirtualPrinterName,
						new ElementId(request.PrintSetupIdValue));
					return PrintWorkResult.Waiting(submission);
				}
				return SheetExportUtilities.ExportSingleNativePdf(
					request.SourceDocument,
					sheet,
					folder,
					item.OutputBaseName,
					request.PdfQuality,
					request.AlwaysUseRaster)
					? PrintWorkResult.Success()
					: PrintWorkResult.Failure($"PDF failed: {sheet.SheetNumber}");
			}

			return SheetExportUtilities.ExportSingleDwg(
				request.SourceDocument,
				sheet,
				request.DwgOutputFolder,
				item.OutputBaseName,
				new ElementId(request.DwgExportSetupIdValue),
				out string error)
				? PrintWorkResult.Success()
				: PrintWorkResult.Failure($"DWG failed ({sheet.SheetNumber}): {error}");
		}
		catch (Exception exception)
		{
			return PrintWorkResult.Failure(exception.Message);
		}
	}
}
