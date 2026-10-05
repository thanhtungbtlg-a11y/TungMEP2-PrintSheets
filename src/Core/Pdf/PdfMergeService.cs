using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace TungMEP2.PrintSheets.Pdf;

public sealed class PdfMergeResult
{
	public bool Succeeded { get; }
	public int PageCount { get; }
	public string Error { get; }

	private PdfMergeResult(bool succeeded, int pageCount, string error)
	{
		Succeeded = succeeded;
		PageCount = pageCount;
		Error = error ?? string.Empty;
	}

	public static PdfMergeResult Success(int pageCount) =>
		new PdfMergeResult(true, pageCount, string.Empty);
	public static PdfMergeResult Failure(string error) =>
		new PdfMergeResult(false, 0, error);
}

public sealed class PdfMergeService
{
	public Task<PdfMergeResult> MergeAsync(
		IReadOnlyList<string> orderedInputs,
		string destination,
		CancellationToken cancellationToken)
	{
		if (orderedInputs == null) throw new ArgumentNullException(nameof(orderedInputs));
		if (string.IsNullOrWhiteSpace(destination))
			throw new ArgumentException("Destination is required.", nameof(destination));
		return Task.Run(
			() => Merge(orderedInputs, destination, cancellationToken),
			CancellationToken.None);
	}

	private static PdfMergeResult Merge(
		IReadOnlyList<string> orderedInputs,
		string destination,
		CancellationToken cancellationToken)
	{
		string destinationFolder = Path.GetDirectoryName(Path.GetFullPath(destination));
		Directory.CreateDirectory(destinationFolder);
		string pendingPath = destination + ".tmp-" + Guid.NewGuid().ToString("N");
		string backupPath = destination + ".bak-" + Guid.NewGuid().ToString("N");
		int expectedPages = 0;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (PdfDocument output = new PdfDocument())
			{
				foreach (string inputPath in orderedInputs)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
						throw new FileNotFoundException("A PDF input is missing.", inputPath);
					using (PdfDocument input = PdfReader.Open(
						inputPath,
						PdfDocumentOpenMode.Import))
					{
						for (int pageIndex = 0; pageIndex < input.PageCount; pageIndex++)
						{
							cancellationToken.ThrowIfCancellationRequested();
							output.AddPage(input.Pages[pageIndex]);
							expectedPages++;
						}
					}
				}
				if (expectedPages == 0)
					throw new InvalidDataException("No PDF pages were found.");
				output.Save(pendingPath);
			}

			cancellationToken.ThrowIfCancellationRequested();
			using (PdfDocument validation = PdfReader.Open(
				pendingPath,
				PdfDocumentOpenMode.Import))
			{
				if (validation.PageCount != expectedPages)
					throw new InvalidDataException("Merged PDF page count did not validate.");
			}

			if (File.Exists(destination))
			{
				try
				{
					File.Replace(pendingPath, destination, backupPath, ignoreMetadataErrors: true);
				}
				catch (PlatformNotSupportedException)
				{
					ReplaceWithRollback(pendingPath, destination, backupPath);
				}
				catch (IOException)
				{
					ReplaceWithRollback(pendingPath, destination, backupPath);
				}
			}
			else
			{
				File.Move(pendingPath, destination);
			}
			return PdfMergeResult.Success(expectedPages);
		}
		catch (Exception exception)
		{
			return PdfMergeResult.Failure(exception.Message);
		}
		finally
		{
			TryDelete(pendingPath);
			TryDelete(backupPath);
		}
	}

	private static void ReplaceWithRollback(
		string pendingPath,
		string destination,
		string backupPath)
	{
		File.Move(destination, backupPath);
		try
		{
			File.Move(pendingPath, destination);
		}
		catch
		{
			if (!File.Exists(destination) && File.Exists(backupPath))
				File.Move(backupPath, destination);
			throw;
		}
	}

	private static void TryDelete(string path)
	{
		try { if (File.Exists(path)) File.Delete(path); } catch { }
	}
}
