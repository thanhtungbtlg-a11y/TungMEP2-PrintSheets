using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using VP_RevitAPI.Helper.Sheets;
using VP_RevitAPI.UI;

namespace TungMEP2.PrintSheets;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class Command_PrintSheets : IExternalCommand
{
	public Result Execute(
		ExternalCommandData commandData,
		ref string message,
		ElementSet elements)
	{
		try
		{
			UIDocument uiDocument = commandData.Application.ActiveUIDocument;
			if (uiDocument == null)
			{
				message = "Không có Revit document đang hoạt động.";
				return Result.Failed;
			}

			List<ViewSheet> sheets =
				SheetExportUtilities.ResolveSheetsFromSelectionOrPrintSet(uiDocument);
			UI_PrintSheetsWindow window = new UI_PrintSheetsWindow(uiDocument, sheets);
			IntPtr ownerHandle = commandData.Application.MainWindowHandle;
			if (ownerHandle != IntPtr.Zero)
			{
				new WindowInteropHelper(window).Owner = ownerHandle;
			}
			window.ShowDialog();
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception exception)
		{
			message = exception.Message;
			return Result.Failed;
		}
	}
}
