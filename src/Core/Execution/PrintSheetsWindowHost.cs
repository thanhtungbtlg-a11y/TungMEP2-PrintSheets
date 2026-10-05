using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using VP_RevitAPI.UI;

namespace TungMEP2.PrintSheets.Execution;

public static class PrintSheetsWindowHost
{
	private static readonly Dictionary<Document, UI_PrintSheetsWindow> Windows =
		new Dictionary<Document, UI_PrintSheetsWindow>();

	public static void ShowOrActivate(
		UIApplication application,
		IReadOnlyList<ElementId> selectedSheetIds)
	{
		if (application?.ActiveUIDocument == null)
			throw new InvalidOperationException("No active Revit document.");
		Document document = application.ActiveUIDocument.Document;
		if (Windows.TryGetValue(document, out UI_PrintSheetsWindow existing))
		{
			if (existing.IsVisible)
			{
				existing.Activate();
				return;
			}
			Windows.Remove(document);
		}

		List<ViewSheet> sheets = (selectedSheetIds ?? Array.Empty<ElementId>())
			.Select(id => document.GetElement(id) as ViewSheet)
			.Where(sheet => sheet != null)
			.ToList();
		RevitUiActionBridge bridge = new RevitUiActionBridge();
		ResponsivePrintScheduler scheduler = new ResponsivePrintScheduler();
		UI_PrintSheetsWindow window = new UI_PrintSheetsWindow(
			application.ActiveUIDocument,
			sheets,
			bridge,
			scheduler);
		IntPtr handle = application.MainWindowHandle;
		if (handle != IntPtr.Zero) new WindowInteropHelper(window).Owner = handle;
		Windows[document] = window;
		window.Closed += (_, __) =>
		{
			Windows.Remove(document);
			scheduler.Dispose();
			bridge.Dispose();
		};
		window.Show();
	}
}
