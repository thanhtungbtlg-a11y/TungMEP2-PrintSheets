using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;

namespace TungMEP2.PrintSheets.Execution;

public sealed class RevitUiActionBridge : IExternalEventHandler, IDisposable
{
	private readonly object _gate = new object();
	private readonly Queue<Action<UIApplication>> _actions =
		new Queue<Action<UIApplication>>();
	private readonly ExternalEvent _externalEvent;
	private bool _disposed;

	public RevitUiActionBridge()
	{
		_externalEvent = ExternalEvent.Create(this);
	}

	public void Raise(Action<UIApplication> action)
	{
		if (action == null) throw new ArgumentNullException(nameof(action));
		lock (_gate)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(RevitUiActionBridge));
			_actions.Enqueue(action);
		}
		_externalEvent.Raise();
	}

	public void Execute(UIApplication app)
	{
		while (true)
		{
			Action<UIApplication> action;
			lock (_gate)
			{
				if (_actions.Count == 0) return;
				action = _actions.Dequeue();
			}
			action(app);
		}
	}

	public string GetName() => "TungMEP2 PrintSheets UI action";

	public void Dispose()
	{
		lock (_gate)
		{
			_disposed = true;
			_actions.Clear();
		}
		_externalEvent.Dispose();
	}
}
