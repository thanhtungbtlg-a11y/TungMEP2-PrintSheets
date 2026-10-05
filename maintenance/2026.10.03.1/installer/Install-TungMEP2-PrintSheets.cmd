@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-TungMEP2-PrintSheets.ps1"
if errorlevel 1 (
  echo.
  echo Cai dat that bai.
  pause
  exit /b 1
)
echo.
echo Cai dat thanh cong.
pause
