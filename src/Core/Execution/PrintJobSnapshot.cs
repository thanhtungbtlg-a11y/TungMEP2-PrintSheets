using System;
using System.Collections.Generic;
using System.Linq;

namespace TungMEP2.PrintSheets.Execution;

public sealed class PrintJobSnapshot
{
	private readonly PrintWorkItem[] _items;

	public string DocumentKey { get; }
	public DateTime CreatedUtc { get; }
	public IReadOnlyList<PrintWorkItem> Items => _items;

	public PrintJobSnapshot(string documentKey, IEnumerable<PrintWorkItem> items)
	{
		if (string.IsNullOrWhiteSpace(documentKey))
			throw new ArgumentException("Document key is required.", nameof(documentKey));
		DocumentKey = documentKey;
		_items = (items ?? throw new ArgumentNullException(nameof(items)))
			.OrderBy(item => item.Order)
			.ToArray();
		CreatedUtc = DateTime.UtcNow;
	}
}
