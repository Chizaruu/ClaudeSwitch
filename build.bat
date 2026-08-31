@echo off
REM  One-stop setup for Windows: builds ClaudeRouter (NativeAOT) from source and
REM  installs it. Just double-click this file.
REM
REM  Requires the .NET 10 SDK (https://dotnet.microsoft.com/download) and, for the
REM  NativeAOT build, the "Desktop development with C++" workload (MSVC linker).
setlocal

where dotnet >nul 2>nul
if errorlevel 1 (
  echo.
  echo ERROR: The .NET 10 SDK is required to build ClaudeRouter.
  echo Install it from https://dotnet.microsoft.com/download then run build.bat again.
  pause
  exit /b 1
)

set ARCH=x64
if /I "%PROCESSOR_ARCHITECTURE%"=="ARM64" set ARCH=arm64
if /I "%PROCESSOR_ARCHITEW6432%"=="ARM64" set ARCH=arm64
set RID=win-%ARCH%

echo Building ClaudeRouter for %RID% ...
dotnet publish "%~dp0src" -c Release -r %RID%
if errorlevel 1 (
  echo.
  echo ERROR: Build failed - see the messages above.
  pause
  exit /b 1
)

set PUB=%~dp0src\bin\Release\net10.0\%RID%\publish\ClaudeRouter.exe
if not exist "%PUB%" (
  echo.
  echo ERROR: Build did not produce ClaudeRouter.exe
  pause
  exit /b 1
)

echo Setting everything up...
"%PUB%" setup

echo.
echo Finished. Follow the on-screen box. You can close this window.
pause
