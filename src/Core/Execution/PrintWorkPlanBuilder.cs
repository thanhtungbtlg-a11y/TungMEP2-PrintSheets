using System;
using System.Collections.Generic;
using VP_RevitAPI.Helper.Sheets;

namespace TungMEP2.PrintSheets.Execution;

public static class PrintWorkPlanBuilder
{
	public static IReadOnlyList<PrintWorkItem> Build(
		IReadOnlyList<PrintWorkSource> sources,
		bool exportPdf,
		string pdfOutputFolder,
		bool exportDwg,
		string dwgOutputFolder,
		bool combinePdf,
		string temporaryPdfFolder)
	{
		if (sources == null) throw new ArgumentNullException(nameof(sources));

		List<PrintWorkItem> items = new List<PrintWorkItem>();
		int order = 0;
		if (exportPdf)
		{
			string pdfNamingFolder = combinePdf ? temporaryPdfFolder : pdfOutputFolder;
			AppendFormatItems(
				sources,
				items,
				ref order,
				PrintWorkKind.Pdf,
				pdfNamingFolder,
				".pdf");
		}

		if (exportDwg)
		{
			AppendFormatItems(
				sources,
				items,
				ref order,
				PrintWorkKind.Dwg,
				dwgOutputFolder,
				".dwg");
		}

		return items;
	}

	private static void AppendFormatItems(
		IReadOnlyList<PrintWorkSource> sources,
		ICollection<PrintWorkItem> items,
		ref int order,
		PrintWorkKind kind,
		string outputFolder,
		string extension)
	{
		HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (PrintWorkSource source in sources)
		{
			string baseName = MakeUniqueBaseName(
				source.OutputBaseName,
				usedNames,
				outputFolder,
				extension);
			items.Add(new PrintWorkItem(order++, source.SheetIdValue, kind, baseName));
		}
	}

	private static string MakeUniqueBaseName(
		string baseName,
		ISet<string> usedNames,
		string outputFolder,
		string extension)
	{
		int suffix = 1;
		while (true)
		{
			string rawCandidate = suffix == 1
				? baseName
				: $"{baseName} ({suffix})";
			string candidate = SheetExportUtilities.FitOutputBaseNameForFolder(
				rawCandidate,
				outputFolder,
				extension);
			if (usedNames.Add(candidate)) return candidate;
			suffix++;
		}
	}
}
