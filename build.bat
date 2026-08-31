@echo off
REM  One-stop setup: builds ClaudeRouter.exe from source and installs it.
REM
REM  Just double-click this file. It does everything else automatically -
REM  and if Claude isn't installed on this PC, it downloads and installs it
REM  for you first. (If Claude is already installed, opening it once
REM  beforehand makes setup slightly faster, but it's not required.)
setlocal
set DEST=%LOCALAPPDATA%\ClaudeRouter
if not exist "%DEST%" mkdir "%DEST%"

echo Preparing...
copy /y "%~dp0src\ClaudeRouter.cs" "%DEST%\" >nul
copy /y "%~dp0assets\Personal.ico" "%DEST%\" >nul
copy /y "%~dp0assets\Work.ico"     "%DEST%\" >nul

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo.
  echo ERROR: Could not find the C# compiler on this PC.
  pause
  exit /b 1
)

"%CSC%" /nologo /target:winexe /out:"%DEST%\ClaudeRouter.exe" ^
  /reference:System.Management.dll /reference:System.Windows.Forms.dll ^
  "%DEST%\ClaudeRouter.cs"

if not exist "%DEST%\ClaudeRouter.exe" (
  echo.
  echo ERROR: Build failed - see the messages above.
  pause
  exit /b 1
)

echo Setting everything up...
"%DEST%\ClaudeRouter.exe" setup

echo.
echo Finished. Follow the on-screen box. You can close this window.
pause
