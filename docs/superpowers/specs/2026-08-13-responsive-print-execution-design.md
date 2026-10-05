# TungMEP2-PrintSheets responsive print execution design

Date: 2026-08-13  
Scope: standalone `TungMEP2-PrintSheets` for Revit 2024 only

## Goal

Allow the user to continue normal Revit modeling while a PrintSheets job is running. Printing remains safe and serialized through the Revit API. The main TungMEP2 add-in is explicitly out of scope until the standalone build has been manually proven stable.

## User-visible behavior

- PrintSheets opens as one modeless window instead of a modal dialog.
- Starting a job freezes its document, sheet order, output paths, PDF/DWG settings, printer, print setup and combine choice.
- The source list and export settings remain disabled for the duration of the job. Pause/Resume and Stop remain enabled.
- The user may change views, zoom, pan, inspect properties, draw and edit model elements.
- Revit may still be unresponsive while one individual Revit export/print call is executing. The tool yields between work units and gives user commands priority.
- After each work unit the tool waits for Revit to be idle for approximately 750 ms before requesting the next unit.
- If another document becomes active, the job pauses automatically and resumes only after the original document becomes active again.
- The tool never saves, synchronizes or closes a Revit document.
- Because Revit output cannot be snapshotted independently of the live model, a not-yet-exported sheet reflects the model state at the moment that sheet is exported. The completion summary warns if the document changed during the job.

## Architecture

### Modeless command host

`Command_PrintSheets` owns or activates a single `UI_PrintSheetsWindow`. The command uses `Show()` and assigns Revit's main window as owner without using `ShowDialog()`.

The window never calls document export synchronously from a WPF click handler. It captures an immutable `PrintJobRequest` and delegates execution to a controller.

### Print job controller

`PrintJobController` is a state machine with these states:

- `Ready`
- `WaitingForRevit`
- `Exporting`
- `Paused`
- `Merging`
- `Completed`
- `Stopped`
- `Failed`

Only one job may exist per standalone add-in instance. Each job is bound to the source document identity captured at start.

The controller builds ordered work units. A work unit is exactly one sheet PDF export/print or one sheet DWG export. The controller records completed, failed and pending units without re-sorting them.

### Revit API scheduling

A dedicated `ExternalEvent` handler executes at most one Revit work unit per callback. No Revit API object or call is used from a background thread.

After a callback returns, a scheduler waits until all of these are true before raising the next event:

- the job is running and not paused/stopped;
- the original document is still open and is the active document;
- Revit is not in another command or transaction context;
- at least 750 ms of cooperative idle time has elapsed.

If `ExternalEvent.Raise()` is pending or rejected, the scheduler waits and retries rather than spinning or entering a nested dispatcher frame. Existing `Dispatcher.PushFrame` and `Thread.Sleep` UI pumping are removed from the print execution path.

### Background file work

Background tasks may only perform file-system operations: wait for virtual-printer output, validate PDF completion, merge PDFs, atomically replace the final file and clean staging files. Results are marshaled back to the WPF dispatcher. They never access `Document`, `ViewSheet`, `PrintManager`, `Element` or any other Revit API object.

## PDF behavior

### Individual PDFs

Each selected sheet produces one PDF work unit. Existing overwrite, naming, local staging and long-path handling remain unchanged.

### Combined PDF

Combined mode no longer submits the entire list as one blocking Revit operation.

1. Each sheet is exported or printed to an isolated temporary PDF in the frozen order.
2. Revit yields between sheets.
3. When every required temporary PDF is complete, a background merge creates a temporary combined PDF.
4. The result is validated and atomically replaces the requested destination.
5. Temporary individual PDFs are removed; no individual PDFs remain in the chosen output folder.

The merge component must preserve page order and page content without rasterizing or reprinting the PDF. Its dependencies must be shipped only inside the standalone add-in and must not reference TungMEP2 or the original add-in.

If export is stopped, a sheet fails, merge fails or validation fails, the previous combined PDF is preserved. The job does not report a combined output as successful.

## DWG behavior

DWG remains one file per sheet and uses the selected Revit DWG Export Setup. Each sheet is one work unit and continues to use local staging before replacing files at the target path.

## Pause, resume and stop

- Pause prevents the next work unit from starting. It does not abort an active Revit export call.
- Resume returns the job to `WaitingForRevit`.
- Stop prevents the next work unit. If a virtual printer is finishing its current file, cleanup waits for that file operation to reach a safe boundary.
- Closing the PrintSheets window during a job requests Stop and keeps the controller alive until safe cleanup completes.
- Closing the source document ends the job safely; no event may execute against another document.

## User editing and consistency

Model editing is permitted. User commands always take precedence because print work is requested only during cooperative idle windows.

The job subscribes to document-change notifications only to set a `DocumentChangedDuringRun` flag; it does not block or inspect user edits in detail. The final summary states that sheets may represent different model states when this flag is set.

The user is responsible for not modifying the listed sheets, their title blocks, print sets or export setups during the job. The frozen request prevents UI setting changes from silently altering the current job.

## Error handling

- A failed sheet is recorded and processing continues with the next work unit unless the failure compromises job-wide state.
- A document mismatch, disposed document or invalid external-event context pauses or safely fails the job; it never redirects work to the active document.
- A pending or unavailable Revit context is displayed as `Đang chờ Revit rảnh`, not as an error.
- Completion reports PDF count, DWG count, failed sheets, pending sheets after Stop and whether the document changed.
- Output replacement is atomic where possible. Existing output remains intact until a complete replacement is ready.
- Staging cleanup is best-effort and never deletes outside the job-specific temporary directory.

## Backup and rollback

Before source or packaged artifacts are modified, create:

`backups/before-responsive-print-<timestamp>/`

The backup contains current source files, project/build scripts, DLL, setup EXE, portable ZIP, README and a `Restore-PrintSheetsBackup.ps1` script. Restore requires Revit to be closed, validates its targets and restores only standalone PrintSheets files. TungMEP2 main is not touched.

## Verification

Automated checks cover:

- state-machine transitions;
- one Revit work unit per callback;
- pause/resume/stop boundaries;
- original-document identity enforcement;
- cooperative delay and no nested dispatcher pumping;
- combined PDF page order and removal of temporary files;
- failed merge preserving an existing destination;
- standalone dependency isolation;
- build, manifest, installer and portable-package contracts.

Manual Revit 2024 checks cover:

- changing views and zoom/pan between exports;
- drawing and editing elements while the queue runs;
- a long-running individual sheet still blocks only for that sheet's Revit call;
- Pause, Resume and Stop;
- switching to another document and returning;
- Revit Native PDF, PDF-XChange Lite, DWG and combined PDF;
- sheet order, output quality, overwrite behavior and cleanup;
- setup install-over and backup restore.

## Acceptance criteria

- The standalone tool allows model editing between sheet work units without unsafe cross-thread Revit API calls.
- User activity delays printing instead of being interrupted by it.
- Combined PDF is produced in the frozen sheet order without leaving individual PDFs.
- All prior standalone behavior remains available.
- TungMEP2 main remains byte-for-byte untouched during this phase.
- A tested rollback package exists before the new standalone build is installed.
