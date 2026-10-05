using VP_RevitAPI.Helper.Sheets;

namespace TungMEP2.PrintSheets.Execution;

public sealed class PrintWorkResult
{
	public bool Succeeded { get; }
	public string Error { get; }
	public SheetExportUtilities.VirtualPdfSubmission VirtualSubmission { get; }
	public bool RequiresFileWait => VirtualSubmission != null;

	private PrintWorkResult(
		bool succeeded,
		string error,
		SheetExportUtilities.VirtualPdfSubmission virtualSubmission)
	{
		Succeeded = succeeded;
		Error = error ?? string.Empty;
		VirtualSubmission = virtualSubmission;
	}

	public static PrintWorkResult Success() => new PrintWorkResult(true, string.Empty, null);
	public static PrintWorkResult Failure(string error) => new PrintWorkResult(false, error, null);
	public static PrintWorkResult Waiting(SheetExportUtilities.VirtualPdfSubmission submission) =>
		new PrintWorkResult(false, string.Empty, submission);
}
