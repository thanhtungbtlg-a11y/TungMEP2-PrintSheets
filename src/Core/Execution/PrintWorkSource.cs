using System;

namespace TungMEP2.PrintSheets.Execution;

public sealed class PrintWorkSource
{
	public long SheetIdValue { get; }

	public string OutputBaseName { get; }

	public PrintWorkSource(long sheetIdValue, string outputBaseName)
	{
		if (sheetIdValue <= 0) throw new ArgumentOutOfRangeException(nameof(sheetIdValue));
		SheetIdValue = sheetIdValue;
		OutputBaseName = outputBaseName ?? throw new ArgumentNullException(nameof(outputBaseName));
	}
}
