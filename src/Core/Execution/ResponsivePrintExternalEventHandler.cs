using Autodesk.Revit.UI;

namespace TungMEP2.PrintSheets.Execution;

public sealed class ResponsivePrintExternalEventHandler : IExternalEventHandler
{
	private ResponsivePrintScheduler _scheduler;

	internal void Attach(ResponsivePrintScheduler scheduler) => _scheduler = scheduler;

	public void Execute(UIApplication app)
	{
		// Deliberately exactly one work unit per ExternalEvent callback.
		_scheduler?.ExecuteOne(app);
	}

	public string GetName() => "TungMEP2 responsive PrintSheets";
}
