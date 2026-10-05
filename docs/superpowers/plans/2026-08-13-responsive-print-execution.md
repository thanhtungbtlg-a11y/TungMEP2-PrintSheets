# Responsive Print Execution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make standalone TungMEP2-PrintSheets yield Revit between individual sheet exports so the user can continue modeling, while preserving ordered PDF/DWG output, pause/stop and combined PDF behavior.

**Architecture:** Replace the modal synchronous export loop with one modeless window, an immutable job snapshot, a state controller and an `ExternalEvent` handler that performs at most one Revit work unit per callback. File waiting and PDF merging run outside the Revit API thread; combined mode exports per-sheet temporary PDFs and atomically installs one merged output.

**Tech Stack:** C# 12, .NET Framework 4.8 WPF, Autodesk Revit API 2024, `ExternalEvent`, PowerShell contract tests, PdfSharpCore 1.3.67 with isolated third-party dependencies, Inno Setup 6.

## Global Constraints

- Scope is only `C:\Users\tungnt2303\Downloads\TungMEP2-PrintSheets`; do not modify `tungmep2`.
- Revit 2024 only.
- Never call Revit API objects from a background thread.
- One Revit PDF or DWG sheet work unit per external-event callback.
- User commands take priority; schedule the next work unit only after a 750 ms cooperative delay.
- Freeze document identity, sheet order, paths and export settings at job start.
- Combined mode leaves only the final combined PDF and preserves an existing destination unless replacement fully succeeds.
- Backup and a tested restore script must exist before production source changes.
- The folder is not a Git repository. Do not initialize Git implicitly; use backup/hash checkpoints instead of commit steps.

---

### Task 1: Baseline backup and executable contract

**Files:**
- Create in backup: `backups/before-responsive-print-<timestamp>/Restore-PrintSheetsBackup.ps1`
- Create in backup: `backups/before-responsive-print-<timestamp>/SHA256SUMS.txt`
- Create: `tools/Test-ResponsivePrint.ps1`
- Modify: `tools/Test-Standalone.ps1`

**Interfaces:**
- Consumes: current standalone source, installer, release DLL, setup EXE and portable ZIP.
- Produces: an immutable rollback directory and a RED behavior contract for responsive execution.

- [ ] **Step 1: Record baseline hashes of TungMEP2 main**

Hash every file below `C:\Users\tungnt2303\Downloads\tungmep2\src`, `tools`, `release`, `Build-TungMEP2.ps1` and save the deterministic list outside that folder. Re-run at completion and require an exact match.

- [ ] **Step 2: Create rollback snapshot before modifying production source**

Copy standalone `src`, `tools`, `installer`, `release`, `build`, `dist`, `README.md`, build script and current root ZIP into a timestamped backup. Exclude existing `backups` to prevent recursion. Generate SHA-256 entries for every copied file.

- [ ] **Step 3: Create and test the restore script**

The script accepts `-TargetRoot`, refuses to run while `Revit.exe` exists, verifies the backup manifest, validates that the target directory name is `TungMEP2-PrintSheets`, and restores only the listed standalone paths. Test it against a temporary sandbox, not the live project.

- [ ] **Step 4: Write the failing responsive-print contract**

`Test-ResponsivePrint.ps1` loads the compiled assembly and requires these real types and behaviors:

```powershell
$stateMachine = New-Object 'TungMEP2.PrintSheets.Execution.PrintJobStateMachine'
Assert-True ($stateMachine.State.ToString() -eq 'Ready') 'job starts Ready'
$stateMachine.Start(3)
Assert-True ($stateMachine.TryBeginNext()) 'first unit can start'
$stateMachine.CompleteCurrent($true)
$stateMachine.Pause()
Assert-True (-not $stateMachine.TryBeginNext()) 'paused job cannot start a unit'
$stateMachine.Resume()
$stateMachine.RequestStop()
Assert-True (-not $stateMachine.TryBeginNext()) 'stopped job cannot start a unit'
```

The test also requires modeless host, external-event handler, `CooperativeDelayMilliseconds = 750`, one-unit execution, document identity guard, `PdfMergeService`, and absence of `Dispatcher.PushFrame` in the compiled execution path.

- [ ] **Step 5: Run RED**

Run: `powershell -ExecutionPolicy Bypass -File .\Build-TungMEP2-PrintSheets.ps1`, then `powershell -ExecutionPolicy Bypass -File .\tools\Test-ResponsivePrint.ps1`  
Expected: FAIL because the state machine and responsive host do not exist and the command remains modal.

Checkpoint: preserve RED output in `reports/responsive-print-red.txt`.

---

### Task 2: Pure job state and immutable work queue

**Files:**
- Create: `src/Core/Execution/PrintJobState.cs`
- Create: `src/Core/Execution/PrintWorkKind.cs`
- Create: `src/Core/Execution/PrintWorkItem.cs`
- Create: `src/Core/Execution/PrintJobSnapshot.cs`
- Create: `src/Core/Execution/PrintJobStateMachine.cs`
- Modify: `tools/Test-ResponsivePrint.ps1`

**Interfaces:**
- Produces: `PrintJobStateMachine.Start(int)`, `TryBeginNext()`, `CompleteCurrent(bool)`, `Pause()`, `Resume()`, `RequestStop()`, `Fail(string)`; immutable `PrintWorkItem` values identified by sheet element-id value and output kind.
- Consumes: no Revit API types, allowing deterministic reflection tests.

- [ ] **Step 1: Extend RED tests for every legal transition**

Assert Ready → WaitingForRevit → Exporting → WaitingForRevit, Pause/Resume, Stop, Failed, exact completed/failed/pending counts and rejection of double-start or completion without an active unit.

- [ ] **Step 2: Run RED and confirm missing behavior**

Expected: FAIL on missing types/methods, not on assembly loading.

- [ ] **Step 3: Implement the minimal state machine**

Use a lock around state transitions, never expose mutable queue collections, and raise plain .NET progress events containing primitive/string data only.

- [ ] **Step 4: Run focused GREEN**

Run build and `tools/Test-ResponsivePrint.ps1`. Expected: state-machine section PASS; later modeless/external-event requirements remain RED.

Checkpoint: save build/test output in `reports/responsive-print-state-green.txt`.

---

### Task 3: Modeless window and safe Revit action bridges

**Files:**
- Create: `src/Core/Execution/PrintSheetsWindowHost.cs`
- Create: `src/Core/Execution/RevitUiActionBridge.cs`
- Create: `src/Core/Execution/ResponsivePrintExternalEventHandler.cs`
- Create: `src/Core/Execution/ResponsivePrintScheduler.cs`
- Modify: `src/Command_PrintSheets.cs`
- Modify: `src/Application.cs`
- Modify: `src/Core/UI/UI_PrintSheetsWindow.cs`
- Modify: `tools/Test-ResponsivePrint.ps1`

**Interfaces:**
- Produces: `PrintSheetsWindowHost.ShowOrActivate(UIApplication, IReadOnlyList<ElementId>)`; `RevitUiActionBridge.Raise(Action<UIApplication>)`; `ResponsivePrintScheduler.CooperativeDelayMilliseconds = 750`.
- Consumes: job snapshot/state machine from Task 2.

- [ ] **Step 1: Write RED contracts for modeless lifetime and event isolation**

Require `Command_PrintSheets` to call the host rather than `ShowDialog`, one host instance per source document, and an external-event handler whose `Execute` processes no more than one work item.

- [ ] **Step 2: Implement the modeless host**

Set Revit main-window ownership, use `Show()`, activate an existing window for the same document, and refuse replacement while its job is active.

- [ ] **Step 3: Route modeless UI Revit access through API context**

Move source refresh, Print Set/Schedule resolution, STT writes and all document reads triggered after the command returns into `RevitUiActionBridge`. UI rows retain primitive display fields and `ElementId` values; WPF bindings never query live `ViewSheet` properties.

- [ ] **Step 4: Implement cooperative scheduling**

Use a dispatcher timer only to decide when to call `ExternalEvent.Raise()`. Do not sleep or push nested dispatcher frames. Treat `Pending`/`Denied` raise results as wait states. Verify original document reference and `IsValidObject` in the handler before resolving each element id.

- [ ] **Step 5: Run focused GREEN**

Run build plus responsive test. Expected: modeless, bridge, delay and one-unit contracts PASS.

Checkpoint: `reports/responsive-print-modeless-green.txt`.

---

### Task 4: Incremental PDF/DWG work execution

**Files:**
- Create: `src/Core/Execution/PrintJobRequest.cs`
- Create: `src/Core/Execution/PrintWorkResult.cs`
- Create: `src/Core/Execution/RevitPrintWorkExecutor.cs`
- Create: `src/Core/Execution/VirtualPdfOutputMonitor.cs`
- Modify: `src/Core/Helper/Sheets/SheetExportUtilities.cs`
- Modify: `src/Core/UI/UI_PrintSheetsWindow.cs`
- Modify: `tools/Test-ResponsivePrint.ps1`
- Modify: `tools/Test-Standalone.ps1`

**Interfaces:**
- Produces: `RevitPrintWorkExecutor.ExecuteOne(UIApplication, PrintJobRequest, PrintWorkItem)` returning `PrintWorkResult`; `VirtualPdfOutputMonitor.WaitAsync(...)` performing file-only work.
- Consumes: frozen request with source document, sheet id values, paths, PDF engine, DPI/raster flags, print setup id and DWG setup id.

- [ ] **Step 1: Write RED contracts for one-sheet native PDF, virtual PDF and DWG dispatch**

Require each executor call to resolve exactly one sheet id, preserve frozen order and return before scheduling another unit. Require the virtual printer API submission and file waiting to be separate methods.

- [ ] **Step 2: Split existing utility methods at the API/file boundary**

Expose focused internal operations for one native PDF, one virtual-printer submission and one DWG. Retain current local staging, overwrite, selected DWG setup, selection suppression and PDF-XChange autosave behavior.

- [ ] **Step 3: Move virtual-printer polling off the Revit thread**

After `SubmitPrint()` returns, monitor only paths/length/timestamps/`%%EOF` on a background task. Marshal the result back before the controller advances.

- [ ] **Step 4: Replace the synchronous window export loop**

`OnExportClick` validates and freezes the request, starts the controller and returns immediately. Progress callbacks update the modeless window. Completion performs the existing summary/open-folder behavior; the window does not auto-close until the job reaches a terminal state.

- [ ] **Step 5: Verify pause/stop and original document behavior**

Run contract tests for pause before next unit, stop during file wait, document switch, closed document and one failed sheet followed by the next sheet.

Checkpoint: `reports/responsive-print-incremental-green.txt`.

---

### Task 5: Background combined-PDF merge and atomic replacement

**Files:**
- Create: `src/Core/Pdf/PdfMergeService.cs`
- Create: `lib/PdfSharpCore.dll`
- Create: `lib/SixLabors.Fonts.dll`
- Create: `lib/SixLabors.ImageSharp.dll`
- Create: `lib/ICSharpCode.SharpZipLib.dll`
- Create: `docs/THIRD-PARTY-NOTICES.txt`
- Modify: `src/TungMEP2.PrintSheets.csproj`
- Modify: `Build-TungMEP2-PrintSheets.ps1`
- Modify: `installer/TungMEP2-PrintSheets.iss`
- Modify: `src/Core/Execution/PrintJobRequest.cs`
- Modify: `src/Core/UI/UI_PrintSheetsWindow.cs`
- Modify: `tools/Test-ResponsivePrint.ps1`
- Modify: `tools/Test-Standalone.ps1`

**Interfaces:**
- Produces: `Task<PdfMergeResult> PdfMergeService.MergeAsync(IReadOnlyList<string> orderedInputs, string destination, CancellationToken token)`.
- Consumes: job-specific temporary PDF paths in frozen order.

- [ ] **Step 1: Vendor isolated third-party binaries**

Copy the already-audited PdfSharpCore 1.3.67 and its exact referenced dependencies into this standalone project's own `lib` directory. Reference and package those copies; do not resolve them from TungMEP2 at runtime. Record names, versions and licenses.

- [ ] **Step 2: Write RED merge tests using hand-created two-page fixtures**

Assert input order, page count, destination replacement only after success, preservation of an existing destination on corrupt input/cancellation, and cleanup limited to a job-specific temp directory.

- [ ] **Step 3: Implement file-only merge**

Open each input in import mode, append pages in supplied order, save to `<destination>.tmp-<guid>`, validate it can be reopened and has the expected page count, then atomically replace/move to destination.

- [ ] **Step 4: Integrate combined mode**

Always create per-sheet temporary PDFs for Native and virtual printers. After all PDF work units succeed, move the state to `Merging`, run `MergeAsync`, then delete temporary inputs. If any required sheet failed or the job stopped, do not replace the old combined file.

- [ ] **Step 5: Run merge and packaging GREEN**

Require portable/EXE payloads to contain all merge dependencies, no Revit API DLL and no `TungMEP2.dll`/`VP_RevitAPI.dll` reference.

Checkpoint: `reports/responsive-print-merge-green.txt`.

---

### Task 6: Full regression, release candidate and rollback verification

**Files:**
- Modify: `README.md`
- Modify: `installer/README.txt`
- Modify: `tools/Test-Standalone.ps1`
- Create: `docs/testing/responsive-print-revit-2024-checklist.md`
- Output: `dist/TungMEP2-PrintSheets-Setup-Revit2024.exe`
- Output: `dist/TungMEP2-PrintSheets-Portable-Revit2024.zip`

**Interfaces:**
- Consumes: completed responsive implementation and backup from Task 1.
- Produces: installable standalone release candidate plus manual Revit checklist.

- [ ] **Step 1: Update user documentation**

Explain modeless behavior, short unavoidable freezes per sheet, user-edit priority, live-model consistency warning, Pause/Stop boundaries, document switching and combined-PDF staging.

- [ ] **Step 2: Run the full automated gate**

Run build, `Test-ResponsivePrint.ps1`, `Test-Standalone.ps1`, installer compilation, portable extraction and manifest verification. Expected: zero failures.

- [ ] **Step 3: Verify backup restoration in a sandbox**

Run the backup restore script against a sandbox containing altered sentinel files. Require restored hashes to match `SHA256SUMS.txt`; do not restore over the live project.

- [ ] **Step 4: Prove TungMEP2 main is untouched**

Recompute the baseline manifest from Task 1 and require exact equality.

- [ ] **Step 5: Install over the current standalone add-in**

Only after confirming `Revit.exe` is closed, run the new setup silently, verify installed hashes and manifest, and preserve the previous installer in the backup.

- [ ] **Step 6: Create the manual Revit 2024 checklist**

Cover Native PDF, PDF-XChange Lite, DWG, combined order/no individual files, active modeling between sheets, pause/resume/stop, document switching, old destination preservation and output quality. Leave its status `NOT RUN` until tested in Revit.

Checkpoint: final report lists artifact links, SHA-256, automated results, install result, backup/restore path and the remaining interactive checks.
