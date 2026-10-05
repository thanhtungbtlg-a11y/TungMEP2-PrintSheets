TungMEP2 PrintSheets 2026.10.03.1 - Revit 2024 only

This is the manual-STT engine restored from the verified 01/08 backup,
with three scoped maintenance fixes. It is NOT the newer modeless engine.

1. Keep separate source groups. Each sheet can be checked in one group only.
   Checking another occurrence unchecks the previous occurrence. Select-all
   selects each sheet once. PDF # follows the checked displayed order.
2. Draft STT and Apply STT remain separate. Export never auto-writes STT.
3. PDF/DWG are generated in short local Temp staging, then safely transferred
   to long local, mapped-drive or UNC destinations using Unicode Windows I/O.
   Normal completion cleans the staging job. Transfer does not retain a second
   permanent copy. A process crash can still leave its unfinished Temp job.
   Windows/server component, access and disk limits still apply.

Close Revit, run the EXE or Install-TungMEP2-PrintSheets.cmd, then restart Revit.
The installed engine is in a hash-specific folder. Previous hash folders are
kept for rollback. EXE and portable package contain the same engine.
The command window title shows 2026.10.03.1.

Source/build: maintenance\2026.10.03.1\Build-Maintenance.ps1
Manual checks in Revit are still required for real printer/plotter behavior.
