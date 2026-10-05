using System;

namespace TungMEP2.PrintSheets.Execution;

public sealed class PrintWorkItem
{
	public int Order { get; }
	public long SheetIdValue { get; }
	public PrintWorkKind Kind { get; }
	public string OutputBaseName { get; }
	public string TemporaryPdfPath { get; }

	public PrintWorkItem(
		int order,
		long sheetIdValue,
		PrintWorkKind kind,
		string outputBaseName,
		string temporaryPdfPath = "")
	{
		if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
		if (sheetIdValue <= 0) throw new ArgumentOutOfRangeException(nameof(sheetIdValue));
		Order = order;
		SheetIdValue = sheetIdValue;
		Kind = kind;
		OutputBaseName = outputBaseName ?? throw new ArgumentNullException(nameof(outputBaseName));
		TemporaryPdfPath = temporaryPdfPath ?? string.Empty;
	}
}
