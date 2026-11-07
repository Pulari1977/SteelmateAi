@echo off
cd /d "%~dp0"
set /p TV=Tekla NuGet version [2026.0.0]: 
if "%TV%"=="" set TV=2026.0.0
dotnet build MyTools.csproj -c Release -p:TeklaVersion=%TV% -o ..
if errorlevel 1 (echo BUILD FAILED & pause & exit /b 1)
echo Done. Restart SteelMate AI to load your tools.
pause
