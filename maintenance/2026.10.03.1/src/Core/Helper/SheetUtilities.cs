using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;

namespace VP_RevitAPI.Helper;

public static class SheetUtilities
{
	public static int SetSttValues(
		Document document,
		IEnumerable<KeyValuePair<ViewSheet, string>> assignments)
	{
		if (document == null)
		{
			throw new ArgumentNullException(nameof(document));
		}
		if (assignments == null)
		{
			throw new ArgumentNullException(nameof(assignments));
		}

		List<KeyValuePair<ViewSheet, string>> values = assignments
			.Where(item =>
				item.Key != null &&
				!item.Key.IsPlaceholder &&
				item.Key.Document.Equals(document))
			.GroupBy(item => item.Key.Id.Value)
			.Select(group => group.First())
			.Select(item => new KeyValuePair<ViewSheet, string>(
				item.Key,
				item.Value?.Trim() ?? string.Empty))
			.ToList();
		if (values.Count == 0)
		{
			throw new InvalidOperationException(
				"Không có Sheet hợp lệ để ghi STT.");
		}

		List<Parameter> parameters = new List<Parameter>();
		foreach (KeyValuePair<ViewSheet, string> item in values)
		{
			if (string.IsNullOrWhiteSpace(item.Value))
			{
				throw new InvalidOperationException(
					$"STT của sheet '{item.Key.SheetNumber}' đang để trống.");
			}

			Parameter parameter = item.Key.LookupParameter("STT");
			if (parameter == null)
			{
				throw new InvalidOperationException(
					$"Sheet '{item.Key.SheetNumber} - {item.Key.Name}' không có Parameter 'STT'.");
			}
			if (parameter.StorageType != StorageType.String)
			{
				throw new InvalidOperationException(
					$"Parameter 'STT' của sheet '{item.Key.SheetNumber}' phải là Text (String).");
			}
			if (parameter.IsReadOnly)
			{
				throw new InvalidOperationException(
					$"Parameter 'STT' của sheet '{item.Key.SheetNumber}' đang ở trạng thái chỉ đọc.");
			}
			parameters.Add(parameter);
		}

		using Transaction transaction =
			new Transaction(document, "PrintSheets - Set STT theo danh sách");
		try
		{
			transaction.Start();
			for (int i = 0; i < values.Count; i++)
			{
				parameters[i].Set(values[i].Value);
			}
			transaction.Commit();
		}
		catch
		{
			if (transaction.GetStatus() == TransactionStatus.Started)
			{
				transaction.RollBack();
			}
			throw;
		}

		return values.Count;
	}

	public static int EnsureSttForExport(
		Document document,
		IEnumerable<ViewSheet> selectedSheets,
		int start = 1)
	{
		if (document == null)
		{
			throw new ArgumentNullException(nameof(document));
		}
		if (selectedSheets == null)
		{
			throw new ArgumentNullException(nameof(selectedSheets));
		}
		if (start < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(start),
				"STT tự động phải bắt đầu từ 1 trở lên.");
		}

		List<ViewSheet> sheets = selectedSheets
			.Where(sheet =>
				sheet != null &&
				!sheet.IsPlaceholder &&
				sheet.Document.Equals(document))
			.GroupBy(sheet => sheet.Id.Value)
			.Select(group => group.First())
			.ToList();
		if (sheets.Count == 0)
		{
			throw new InvalidOperationException(
				"Không tìm thấy Sheet hợp lệ để kiểm tra STT trước khi xuất.");
		}

		// STT follows the exact order shown in PrintSheets.
		List<Parameter> sttParameters = sheets
			.Select(sheet => sheet.LookupParameter("STT"))
			.ToList();
		bool hasUnsupportedParameter = sttParameters.Any(parameter =>
			parameter == null ||
			parameter.StorageType != StorageType.String);
		bool requiresRepair = hasUnsupportedParameter ||
			RequiresAutomaticSttRepair(
				sttParameters.Select(parameter =>
					parameter?.StorageType == StorageType.String
						? parameter.AsString()
						: null),
				start);
		if (!requiresRepair)
		{
			return 0;
		}

		for (int i = 0; i < sheets.Count; i++)
		{
			ViewSheet sheet = sheets[i];
			Parameter stt = sttParameters[i];
			if (stt == null)
			{
				throw new InvalidOperationException(
					"Sheet '" + sheet.SheetNumber + " - " + sheet.Name +
					"' không có Parameter 'STT'.");
			}
			if (stt.StorageType != StorageType.String)
			{
				throw new InvalidOperationException(
					"Parameter 'STT' của sheet '" + sheet.SheetNumber +
					"' phải là Text (String).");
			}
			if (stt.IsReadOnly)
			{
				throw new InvalidOperationException(
					"Parameter 'STT' của sheet '" + sheet.SheetNumber +
					"' đang ở trạng thái chỉ đọc nên PrintSheets không thể tự sửa.");
			}
		}

		using Transaction transaction =
			new Transaction(document, "PrintSheets - Auto Set STT");
		try
		{
			transaction.Start();
			for (int i = 0; i < sheets.Count; i++)
			{
				sheets[i]
					.LookupParameter("STT")
					.Set((start + i).ToString(CultureInfo.InvariantCulture));
			}
			transaction.Commit();
		}
		catch
		{
			if (transaction.GetStatus() == TransactionStatus.Started)
			{
				transaction.RollBack();
			}
			throw;
		}

		return sheets.Count;
	}

	internal static bool RequiresAutomaticSttRepair(
		IEnumerable<string> rawValues,
		int start)
	{
		if (rawValues == null || start < 1)
		{
			return true;
		}

		int expectedValue = start;
		foreach (string rawValue in rawValues)
		{
			if (string.IsNullOrWhiteSpace(rawValue))
			{
				return true;
			}

			string trimmedValue = rawValue.Trim();
			if ((!int.TryParse(
					trimmedValue,
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int sttValue) &&
				!int.TryParse(
					trimmedValue,
					NumberStyles.Integer,
					CultureInfo.CurrentCulture,
					out sttValue)) ||
				sttValue != expectedValue)
			{
				return true;
			}

			expectedValue++;
		}

		return false;
	}

	private static int CompareSheetNumberNatural(string left, string right)
	{
		left = left ?? string.Empty;
		right = right ?? string.Empty;
		if (ReferenceEquals(left, right))
		{
			return 0;
		}

		int leftIndex = 0;
		int rightIndex = 0;
		while (leftIndex < left.Length && rightIndex < right.Length)
		{
			bool leftIsDigit = char.IsDigit(left[leftIndex]);
			bool rightIsDigit = char.IsDigit(right[rightIndex]);
			if (leftIsDigit && rightIsDigit)
			{
				int leftStart = leftIndex;
				int rightStart = rightIndex;
				while (leftIndex < left.Length && char.IsDigit(left[leftIndex]))
				{
					leftIndex++;
				}
				while (rightIndex < right.Length && char.IsDigit(right[rightIndex]))
				{
					rightIndex++;
				}

				string leftDigits = left.Substring(leftStart, leftIndex - leftStart);
				string rightDigits = right.Substring(rightStart, rightIndex - rightStart);
				string leftSignificant = leftDigits.TrimStart('0');
				string rightSignificant = rightDigits.TrimStart('0');
				leftSignificant = leftSignificant.Length == 0 ? "0" : leftSignificant;
				rightSignificant = rightSignificant.Length == 0 ? "0" : rightSignificant;
				int lengthComparison =
					leftSignificant.Length.CompareTo(rightSignificant.Length);
				if (lengthComparison != 0)
				{
					return lengthComparison;
				}

				int digitComparison = string.CompareOrdinal(
					leftSignificant,
					rightSignificant);
				if (digitComparison != 0)
				{
					return digitComparison;
				}

				int originalLengthComparison =
					leftDigits.Length.CompareTo(rightDigits.Length);
				if (originalLengthComparison != 0)
				{
					return originalLengthComparison;
				}
			}
			else
			{
				if (leftIsDigit != rightIsDigit)
				{
					return leftIsDigit ? -1 : 1;
				}

				int characterComparison = char
					.ToUpperInvariant(left[leftIndex])
					.CompareTo(char.ToUpperInvariant(right[rightIndex]));
				if (characterComparison != 0)
				{
					return characterComparison;
				}
				leftIndex++;
				rightIndex++;
			}
		}

		return left.Length.CompareTo(right.Length);
	}
}
