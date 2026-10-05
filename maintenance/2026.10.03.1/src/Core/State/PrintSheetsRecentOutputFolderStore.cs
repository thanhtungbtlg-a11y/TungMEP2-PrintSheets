using System;
using System.IO;
using System.Text;

namespace VP_RevitAPI.State;

public static class PrintSheetsRecentOutputFolderStore
{
	private static readonly object LockObject = new object();

	public static string Load()
	{
		return LoadPdfOutputFolder();
	}

	public static string LoadPdfOutputFolder()
	{
		return LoadPath("recent_pdf_folder.txt");
	}

	public static string LoadDwgOutputFolder()
	{
		return LoadPath("recent_dwg_folder.txt");
	}

	public static void Save(string folderPath)
	{
		Save(folderPath, folderPath);
	}

	public static void Save(string pdfFolderPath, string dwgFolderPath)
	{
		lock (LockObject)
		{
			SavePath("recent_pdf_folder.txt", pdfFolderPath);
			SavePath("recent_dwg_folder.txt", dwgFolderPath);
		}
	}

	private static string LoadPath(string fileName)
	{
		lock (LockObject)
		{
			try
			{
				string filePath = Path.Combine(GetStorageDirectoryPath(), fileName);
				if (File.Exists(filePath))
				{
					string savedPath = File.ReadAllText(filePath, Encoding.UTF8).Trim();
					if (!string.IsNullOrWhiteSpace(savedPath))
					{
						return savedPath;
					}
				}
			}
			catch
			{
			}

			return GetDefaultOutputFolderPath();
		}
	}

	private static void SavePath(string fileName, string folderPath)
	{
		string normalizedPath = folderPath?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(normalizedPath))
		{
			return;
		}

		string tempPath = string.Empty;
		try
		{
			string storageDirectory = GetStorageDirectoryPath();
			Directory.CreateDirectory(storageDirectory);
			string filePath = Path.Combine(storageDirectory, fileName);
			tempPath = filePath + ".tmp";
			File.WriteAllText(tempPath, normalizedPath, Encoding.UTF8);
			if (File.Exists(filePath))
			{
				File.Replace(tempPath, filePath, null);
			}
			else
			{
				File.Move(tempPath, filePath);
			}
		}
		catch
		{
		}
		finally
		{
			if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
			{
				try
				{
					File.Delete(tempPath);
				}
				catch
				{
				}
			}
		}
	}

	private static string GetStorageDirectoryPath()
	{
		string localAppData = Environment.GetFolderPath(
			Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(
			localAppData,
			"TungMEP2-PrintSheets",
			"State");
	}

	private static string GetDefaultOutputFolderPath()
	{
		string desktop = Environment.GetFolderPath(
			Environment.SpecialFolder.DesktopDirectory);
		return Path.Combine(desktop, "TungMEP2 PrintSheets");
	}
}
