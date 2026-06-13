@echo off
setlocal
rem Install the wrapper into RadiKool's libs folder.
rem   1) rename real ffmpeg.exe -> ffmpeg.origin.exe, ffplay.exe -> ffplay.origin.exe
rem   2) copy ffmpeg.wrapper.exe to BOTH ffmpeg.exe and ffplay.exe
rem Run this in the libs folder. If it fails, right-click -> Run as administrator.

cd /d "%~dp0"

rem RadiKool and any running player/recorder LOCK the exe files, so they must be closed first.
call :checkproc Radikool.exe || goto :busy
call :checkproc ffplay.exe   || goto :busy
call :checkproc ffmpeg.exe   || goto :busy

if not exist "ffmpeg.wrapper.exe" (
  echo [ERROR] ffmpeg.wrapper.exe not found. Put it in RadiKool's libs folder.
  pause
  exit /b 1
)
if exist "ffmpeg.origin.exe" (
  echo [ERROR] ffmpeg.origin.exe already exists. Already installed? Run unwrap.bat first.
  pause
  exit /b 1
)
if exist "ffplay.origin.exe" (
  echo [ERROR] ffplay.origin.exe already exists. Already installed? Run unwrap.bat first.
  pause
  exit /b 1
)
if not exist "ffmpeg.exe" (
  echo [ERROR] real ffmpeg.exe not found. Run this in RadiKool's libs folder.
  pause
  exit /b 1
)
if not exist "ffplay.exe" (
  echo [ERROR] real ffplay.exe not found. Run this in RadiKool's libs folder.
  pause
  exit /b 1
)

ren "ffmpeg.exe" "ffmpeg.origin.exe"
if errorlevel 1 (
  echo [ERROR] Failed to rename ffmpeg.exe. Close RadiKool/ffmpeg or Run as administrator.
  pause
  exit /b 1
)
ren "ffplay.exe" "ffplay.origin.exe"
if errorlevel 1 (
  echo [ERROR] Failed to rename ffplay.exe. Reverting ffmpeg.
  ren "ffmpeg.origin.exe" "ffmpeg.exe"
  pause
  exit /b 1
)

copy /Y "ffmpeg.wrapper.exe" "ffmpeg.exe" >nul
if errorlevel 1 (
  echo [ERROR] Failed to copy wrapper to ffmpeg.exe.
  pause
  exit /b 1
)
copy /Y "ffmpeg.wrapper.exe" "ffplay.exe" >nul
if errorlevel 1 (
  echo [ERROR] Failed to copy wrapper to ffplay.exe.
  pause
  exit /b 1
)

echo Done.
echo   record: ffmpeg.exe (wrapper) -^> ffmpeg.origin.exe (real)
echo   play  : ffplay.exe (wrapper) -^> ffplay.origin.exe (real)
echo Start RadiKool; LR stations (Maebashi etc.) will play and record.
echo To revert, run unwrap.bat
pause
endlocal
exit /b 0

:checkproc
tasklist /FI "IMAGENAME eq %~1" 2>nul | find /I "%~1" >nul
if not errorlevel 1 (
  echo [ERROR] %~1 is running. Close RadiKool and stop play/record, then run again.
  exit /b 1
)
exit /b 0

:busy
pause
exit /b 1
