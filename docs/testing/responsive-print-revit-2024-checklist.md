# TungMEP2 PrintSheets — Revit 2024 interactive checklist

Status: **NOT RUN** — requires direct operation inside Revit 2024.

Use a disposable copy of a project containing at least three printable sheets.

- [ ] Open Print Sheets. Verify the window is modeless and Revit remains selectable.
- [ ] Native PDF 300 DPI: export 3 sheets and change view/model between sheets.
- [ ] Native PDF 600 DPI: verify title block, colors, lineweights and fonts.
- [ ] PDF-XChange Lite: verify no second Save As prompt and completion only after `%%EOF`.
- [ ] Pause after the first sheet; verify no next sheet starts until Resume.
- [ ] Stop during the wait between sheets; verify no later sheet is submitted.
- [ ] Switch to another open model; verify the job waits and resumes only in the source model.
- [ ] Export individual PDF; verify overwrite and visible order.
- [ ] Combine PDF; verify page order and that individual PDFs are absent from the output folder.
- [ ] Put an old combined PDF at the destination, force a failed/stopped run, and verify it remains intact.
- [ ] Export DWG with the chosen setup; verify title block, layers, colors and lineweights.
- [ ] Export PDF + DWG together to separate folders.
- [ ] Verify the dialog closes only after the terminal success/error/stop notification.
- [ ] Verify TungMEP2 full add-in still loads independently and is unchanged.

Expected limitation: Revit can pause briefly while the current single-sheet
`Document.Export` or `PrintManager.SubmitPrint` call is executing. Responsiveness
is guaranteed between sheet work units, not inside Autodesk's synchronous call.
