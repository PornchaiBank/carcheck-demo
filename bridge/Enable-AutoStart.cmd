@echo off
setlocal
cd /d "%~dp0"

if not exist "app\CardBridge.exe" (
 echo Card Bridge is not ready. Run Build.cmd first.
 pause
 exit /b 1
)

if not exist "Start-CardBridge-Silent.vbs" (
 echo Silent launcher is missing. Download and extract the latest package again.
 pause
 exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$launcher=(Resolve-Path '.\Start-CardBridge-Silent.vbs').Path; Get-ChildItem -LiteralPath '.\app' -File | Unblock-File; Unblock-File -LiteralPath $launcher; $q=[char]34; $command='wscript.exe '+$q+$launcher+$q; New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Force | Out-Null; Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'CARCHECKCardBridge' -Value $command; $old=Join-Path ([Environment]::GetFolderPath('Startup')) 'CARCHECK Card Bridge.lnk'; if(Test-Path -LiteralPath $old){Remove-Item -LiteralPath $old -Force}"
if errorlevel 1 (
 echo Cannot enable automatic startup.
 pause
 exit /b 1
)

start "" wscript.exe "%~dp0Start-CardBridge-Silent.vbs"

echo Automatic startup is enabled for the current Windows user.
echo Card Bridge has been started in the background and will start silently after each Windows sign-in.
echo Keep this folder in its current location.
echo Test the connection at http://127.0.0.1:17861
pause
