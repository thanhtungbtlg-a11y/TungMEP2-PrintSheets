using System;
using System.Threading;
using System.Threading.Tasks;
using VP_RevitAPI.Helper.Sheets;

namespace TungMEP2.PrintSheets.Execution;

public sealed class VirtualPdfOutputMonitor
{
	public Task<PrintWorkResult> WaitAsync(
		SheetExportUtilities.VirtualPdfSubmission submission,
		CancellationToken cancellationToken)
	{
		if (submission == null) throw new ArgumentNullException(nameof(submission));
		return Task.Run(() =>
		{
			try
			{
				bool succeeded = SheetExportUtilities.CompleteVirtualPdfOutput(
					submission,
					() => !cancellationToken.IsCancellationRequested);
				return succeeded
					? PrintWorkResult.Success()
					: PrintWorkResult.Failure(
						$"Printer '{submission.PrinterName}' did not create a complete PDF.");
			}
			catch (OperationCanceledException)
			{
				return PrintWorkResult.Failure("PRINT_JOB_STOPPED");
			}
			catch (Exception exception)
			{
				return PrintWorkResult.Failure(exception.Message);
			}
		}, CancellationToken.None);
	}
}
