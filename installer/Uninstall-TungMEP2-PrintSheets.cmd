@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall-TungMEP2-PrintSheets.ps1"
if errorlevel 1 (
  echo.
  echo Go cai dat that bai.
  pause
  exit /b 1
)
echo.
echo Go cai dat thanh cong.
pause
