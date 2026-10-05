using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using TungMEP2.PrintSheets.Pdf;

namespace TungMEP2.PrintSheets.Execution;

public sealed class ResponsivePrintScheduler : IDisposable
{
	public const int CooperativeDelayMilliseconds = 750;

	private readonly ExternalEvent _externalEvent;
	private readonly DispatcherTimer _timer;
	private readonly Dispatcher _dispatcher;
	private readonly RevitPrintWorkExecutor _executor = new RevitPrintWorkExecutor();
	private readonly VirtualPdfOutputMonitor _virtualMonitor = new VirtualPdfOutputMonitor();
	private readonly PdfMergeService _mergeService = new PdfMergeService();
	private readonly List<string> _errors = new List<string>();
	private CancellationTokenSource _cancellation;
	private PrintJobRequest _request;
	private int _nextIndex;
	private bool _raisePending;
	private bool _disposed;
	private bool _mergeStarted;
	private PrintWorkItem _currentItem;

	public PrintJobStateMachine StateMachine { get; private set; } = new PrintJobStateMachine();
	public IReadOnlyList<string> Errors => _errors.ToArray();
	public bool IsWaitingForSourceDocument { get; private set; }
	public event EventHandler ProgressChanged;
	public event EventHandler Finished;

	public ResponsivePrintScheduler()
	{
		_dispatcher = Dispatcher.CurrentDispatcher;
		ResponsivePrintExternalEventHandler handler =
			new ResponsivePrintExternalEventHandler();
		handler.Attach(this);
		_externalEvent = ExternalEvent.Create(handler);
		_timer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(CooperativeDelayMilliseconds)
		};
		_timer.Tick += OnTimerTick;
	}

	public void Start(PrintJobRequest request)
	{
		if (request == null) throw new ArgumentNullException(nameof(request));
		if (_request != null && !StateMachine.IsTerminal)
			throw new InvalidOperationException("A print job is already active.");
		_request = request;
		IsWaitingForSourceDocument = false;
		_nextIndex = 0;
		_mergeStarted = false;
		_errors.Clear();
		_cancellation?.Dispose();
		_cancellation = new CancellationTokenSource();
		StateMachine = new PrintJobStateMachine();
		StateMachine.StateChanged += OnStateChanged;
		StateMachine.Start(request.Items.Count, request.CombinePdf);
		_timer.Start();
		ProgressChanged?.Invoke(this, EventArgs.Empty);
	}

	public void Pause() => StateMachine.Pause();
	public void Resume() => StateMachine.Resume();
	public void Stop()
	{
		StateMachine.RequestStop();
		_cancellation?.Cancel();
	}

	private void OnTimerTick(object sender, EventArgs e)
	{
		if (_disposed || _request == null || _raisePending ||
			StateMachine.State != PrintJobState.WaitingForRevit)
			return;
		ExternalEventRequest result = _externalEvent.Raise();
		_raisePending = result == ExternalEventRequest.Accepted ||
			result == ExternalEventRequest.Pending;
	}

	internal void ExecuteOne(UIApplication application)
	{
		_raisePending = false;
		if (_request == null || StateMachine.State != PrintJobState.WaitingForRevit)
			return;
		UIDocument active = application.ActiveUIDocument;
		if (active == null ||
			!NativeObjectIdentity.IsSame(_request.SourceDocument, active.Document))
		{
			IsWaitingForSourceDocument = true;
			ProgressChanged?.Invoke(this, EventArgs.Empty);
			return;
		}
		IsWaitingForSourceDocument = false;
		if (_nextIndex >= _request.Items.Count || !StateMachine.TryBeginNext())
			return;

		PrintWorkItem item = _request.Items[_nextIndex++];
		_currentItem = item;
		PrintWorkResult result = _executor.ExecuteOne(application, _request, item);
		if (result.RequiresFileWait)
		{
			_ = _virtualMonitor.WaitAsync(result.VirtualSubmission, _cancellation.Token)
				.ContinueWith(task =>
				{
					PrintWorkResult completed = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion
						? task.Result
						: PrintWorkResult.Failure(task.Exception?.GetBaseException().Message ?? "PDF wait failed.");
					_dispatcher.BeginInvoke(new Action(() => CompleteUnit(completed)));
				}, CancellationToken.None);
			return;
		}
		CompleteUnit(result);
	}

	private void CompleteUnit(PrintWorkResult result)
	{
		if (!result.Succeeded && !string.IsNullOrWhiteSpace(result.Error))
			_errors.Add(result.Error);
		bool stateMachineSuccess = result.Succeeded ||
			(_request.CombinePdf && _currentItem?.Kind == PrintWorkKind.Dwg);
		StateMachine.CompleteCurrent(stateMachineSuccess);
		_currentItem = null;
		ProgressChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnStateChanged(object sender, EventArgs e)
	{
		ProgressChanged?.Invoke(this, EventArgs.Empty);
		if (StateMachine.State == PrintJobState.Merging && !_mergeStarted)
		{
			_mergeStarted = true;
			List<string> inputs = _request.Items
				.Where(item => item.Kind == PrintWorkKind.Pdf)
				.OrderBy(item => item.Order)
				.Select(item => Path.Combine(
					_request.JobTemporaryFolder,
					item.OutputBaseName + ".pdf"))
				.ToList();
			_ = _mergeService.MergeAsync(
				inputs,
				_request.CombinedPdfPath,
				_cancellation.Token)
				.ContinueWith(task =>
				{
					PdfMergeResult result = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion
						? task.Result
						: PdfMergeResult.Failure(task.Exception?.GetBaseException().Message ?? "PDF merge failed.");
					_dispatcher.BeginInvoke(new Action(() =>
					{
						if (!result.Succeeded && !string.IsNullOrWhiteSpace(result.Error))
							_errors.Add(result.Error);
						if (StateMachine.State == PrintJobState.Merging)
							StateMachine.CompleteMerge(result.Succeeded, result.Error);
					}));
				}, CancellationToken.None);
		}
		if (StateMachine.IsTerminal) Finish();
	}

	private void Finish()
	{
		_timer.Stop();
		if (_request != null && !string.IsNullOrWhiteSpace(_request.JobTemporaryFolder))
		{
			try
			{
				if (Directory.Exists(_request.JobTemporaryFolder))
					Directory.Delete(_request.JobTemporaryFolder, recursive: true);
			}
			catch
			{
			}
		}
		Finished?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_timer.Stop();
		_cancellation?.Cancel();
		_cancellation?.Dispose();
		_externalEvent.Dispose();
	}
}
