@echo off
setlocal
cd /d "%~dp0"

set "deswik_test_shell=pwsh.exe"
where pwsh.exe >nul 2>&1
if errorlevel 1 set "deswik_test_shell=powershell.exe"

"%deswik_test_shell%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0deswik-mcp\tools\Run-DeswikTestSetup.ps1"
set "deswik_test_exit=%ERRORLEVEL%"

if not "%deswik_test_exit%"=="0" (
  echo.
  echo Deswik test setup failed. Review the error above.
  pause
)

exit /b %deswik_test_exit%
