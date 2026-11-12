@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'CARCHECKCardBridge' -ErrorAction SilentlyContinue; $link=Join-Path ([Environment]::GetFolderPath('Startup')) 'CARCHECK Card Bridge.lnk'; if(Test-Path -LiteralPath $link){Remove-Item -LiteralPath $link -Force}"
if errorlevel 1 (
 echo Cannot disable automatic startup.
 pause
 exit /b 1
)

echo Automatic startup is disabled for the current Windows user.
echo The Card Bridge process that is already running was not stopped.
pause
