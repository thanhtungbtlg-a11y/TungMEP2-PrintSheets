using System;

namespace TungMEP2.PrintSheets.Execution;

public sealed class PrintJobStateMachine
{
	private readonly object _gate = new object();
	private bool _activeUnit;
	private bool _pauseRequested;
	private bool _stopRequested;
	private bool _requiresMerge;
	private string _failure = string.Empty;

	public PrintJobState State { get; private set; } = PrintJobState.Ready;
	public int TotalCount { get; private set; }
	public int CompletedCount { get; private set; }
	public int FailedCount { get; private set; }
	public int PendingCount
	{
		get
		{
			lock (_gate)
				return Math.Max(0, TotalCount - CompletedCount - FailedCount - (_activeUnit ? 1 : 0));
		}
	}
	public string Failure => _failure;
	public bool IsTerminal =>
		State == PrintJobState.Completed ||
		State == PrintJobState.Stopped ||
		State == PrintJobState.Failed;

	public event EventHandler StateChanged;

	public void Start(int totalCount, bool requiresMerge = false)
	{
		if (totalCount < 0) throw new ArgumentOutOfRangeException(nameof(totalCount));
		lock (_gate)
		{
			if (State != PrintJobState.Ready)
				throw new InvalidOperationException("PRINT_JOB_ALREADY_STARTED");
			TotalCount = totalCount;
			_requiresMerge = requiresMerge;
			State = totalCount == 0
				? (requiresMerge ? PrintJobState.Merging : PrintJobState.Completed)
				: PrintJobState.WaitingForRevit;
		}
		RaiseStateChanged();
	}

	public bool TryBeginNext()
	{
		lock (_gate)
		{
			if (State != PrintJobState.WaitingForRevit || _activeUnit || _stopRequested)
				return false;
			if (CompletedCount + FailedCount >= TotalCount)
				return false;
			_activeUnit = true;
			State = PrintJobState.Exporting;
		}
		RaiseStateChanged();
		return true;
	}

	public void CompleteCurrent(bool succeeded)
	{
		lock (_gate)
		{
			if (!_activeUnit || State != PrintJobState.Exporting)
				throw new InvalidOperationException("PRINT_JOB_HAS_NO_ACTIVE_UNIT");
			_activeUnit = false;
			if (succeeded) CompletedCount++;
			else FailedCount++;

			if (_stopRequested)
				State = PrintJobState.Stopped;
			else if (CompletedCount + FailedCount >= TotalCount)
				State = _requiresMerge && FailedCount == 0
					? PrintJobState.Merging
					: PrintJobState.Completed;
			else if (_pauseRequested)
				State = PrintJobState.Paused;
			else
				State = PrintJobState.WaitingForRevit;
		}
		RaiseStateChanged();
	}

	public void Pause()
	{
		lock (_gate)
		{
			if (IsTerminal || State == PrintJobState.Merging) return;
			_pauseRequested = true;
			if (!_activeUnit) State = PrintJobState.Paused;
		}
		RaiseStateChanged();
	}

	public void Resume()
	{
		lock (_gate)
		{
			if (State != PrintJobState.Paused || _stopRequested) return;
			_pauseRequested = false;
			State = PrintJobState.WaitingForRevit;
		}
		RaiseStateChanged();
	}

	public void RequestStop()
	{
		lock (_gate)
		{
			if (IsTerminal) return;
			_stopRequested = true;
			_pauseRequested = false;
			if (!_activeUnit) State = PrintJobState.Stopped;
		}
		RaiseStateChanged();
	}

	public void CompleteMerge(bool succeeded, string failure = "")
	{
		lock (_gate)
		{
			if (State != PrintJobState.Merging)
				throw new InvalidOperationException("PRINT_JOB_NOT_MERGING");
			_failure = failure ?? string.Empty;
			State = succeeded ? PrintJobState.Completed : PrintJobState.Failed;
		}
		RaiseStateChanged();
	}

	public void Fail(string failure)
	{
		lock (_gate)
		{
			if (IsTerminal) return;
			_activeUnit = false;
			_failure = failure ?? string.Empty;
			State = PrintJobState.Failed;
		}
		RaiseStateChanged();
	}

	private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
