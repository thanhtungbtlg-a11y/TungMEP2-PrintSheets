namespace TungMEP2.PrintSheets.Execution;

public enum PrintJobState
{
	Ready,
	WaitingForRevit,
	Exporting,
	Paused,
	Merging,
	Completed,
	Stopped,
	Failed
}
