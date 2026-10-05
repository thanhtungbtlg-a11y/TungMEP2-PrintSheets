using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TungMEP2.PrintSheets.Execution;
using VP_RevitAPI.Helper.Sheets;

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
			PrintSheetsWindowHost.ShowOrActivate(
				commandData.Application,
				sheets.Select(sheet => sheet.Id).ToList());
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
