@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 goto missing
dotnet publish "src\CardBridge\CardBridge.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0app"
if errorlevel 1 goto failed
"%~dp0app\CardBridge.exe" --self-test
if errorlevel 1 goto failed
echo Build and APDU self-tests succeeded. Run Run.cmd next.
pause
exit /b 0
:missing
echo Install .NET 10 SDK Windows x64 from https://dotnet.microsoft.com/download/dotnet/10.0
echo Then close and reopen this window and run Build.cmd again.
pause
exit /b 1
:failed
echo Build or self-test failed. Do not continue. Share only the error text, not card data.
pause
exit /b 1
