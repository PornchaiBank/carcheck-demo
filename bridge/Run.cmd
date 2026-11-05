@echo off
cd /d "%~dp0"
if not exist "app\CardBridge.exe" (
 echo Run Build.cmd first.
 pause
 exit /b 1
)
"app\CardBridge.exe"
pause
