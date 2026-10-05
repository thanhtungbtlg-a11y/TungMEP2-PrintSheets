using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace TungMEP2.PrintSheets;

public sealed class Application : IExternalApplication
{
	private const string RibbonTabName = "TungMEP2 Print";
	private const string RibbonPanelName = "Print Sheets";

	public Result OnStartup(UIControlledApplication application)
	{
		try
		{
			try
			{
				application.CreateRibbonTab(RibbonTabName);
			}
			catch (Autodesk.Revit.Exceptions.ArgumentException)
			{
			}

			RibbonPanel panel = application
				.GetRibbonPanels(RibbonTabName)
				.FirstOrDefault(item =>
					string.Equals(item.Name, RibbonPanelName, StringComparison.OrdinalIgnoreCase)) ??
				application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);

			PushButtonData buttonData = new PushButtonData(
				"TungMEP2PrintSheets.Command",
				"Print\nSheets",
				typeof(Application).Assembly.Location,
				typeof(Command_PrintSheets).FullName)
			{
				ToolTip = "Xuất hàng loạt sheet sang PDF và DWG với đường dẫn riêng.",
				LongDescription =
					"Add-in độc lập chỉ chứa PrintSheets: Native PDF hoặc máy in PDF ảo, " +
					"300/600 DPI, Font-safe Raster, combine PDF, DWG Export Setup, " +
					"Auto Set STT 1…n, ghi đè an toàn, tạm dừng và ngưng xuất."
			};

			if (panel.AddItem(buttonData) is PushButton button)
			{
				button.Image = LoadIcon("Printer_16.png");
				button.LargeImage = LoadIcon("Printer_32.png");
			}

			return Result.Succeeded;
		}
		catch (Exception exception)
		{
			TaskDialog.Show(
				"TungMEP2 PrintSheets",
				"Không thể tạo Ribbon PrintSheets.\n" + exception.Message);
			return Result.Failed;
		}
	}

	public Result OnShutdown(UIControlledApplication application)
	{
		return Result.Succeeded;
	}

	private static ImageSource LoadIcon(string fileName)
	{
		string resourceName =
			"TungMEP2.PrintSheets.Resources." + fileName;
		using (Stream stream = typeof(Application)
			.Assembly
			.GetManifestResourceStream(resourceName) ??
			throw new InvalidOperationException(
				"Không tìm thấy icon nhúng: " + resourceName))
		{
			BitmapImage image = new BitmapImage();
			image.BeginInit();
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.StreamSource = stream;
			image.EndInit();
			image.Freeze();
			return image;
		}
	}
}
