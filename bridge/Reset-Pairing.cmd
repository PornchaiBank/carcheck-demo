@echo off
cd /d "%~dp0"
if not exist "app\CardBridge.exe" (
 echo Run Build.cmd first.
 pause
 exit /b 1
)
echo Close the existing bridge before continuing. This revokes old pairing codes.
pause
"app\CardBridge.exe" --allow-carcheck-web --reset-pairing
pause
