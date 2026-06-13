@echo off
setlocal
rem Revert the wrapper: restore real ffmpeg.exe / ffplay.exe from *.origin.exe.

cd /d "%~dp0"

rem RadiKool and any running player/recorder LOCK the exe files, so they must be closed first.
call :checkproc Radikool.exe || goto :busy
call :checkproc ffplay.exe   || goto :busy
call :checkproc ffmpeg.exe   || goto :busy

if not exist "ffmpeg.origin.exe" if not exist "ffplay.origin.exe" (
  echo [INFO] No *.origin.exe found here. Run this in the folder where you ran wrap.
  pause
  exit /b 0
)

set "ERR=0"

if exist "ffmpeg.origin.exe" (
  if exist "ffmpeg.exe" del /F /Q "ffmpeg.exe"
  ren "ffmpeg.origin.exe" "ffmpeg.exe"
  if errorlevel 1 set "ERR=1"
)
if exist "ffplay.origin.exe" (
  if exist "ffplay.exe" del /F /Q "ffplay.exe"
  ren "ffplay.origin.exe" "ffplay.exe"
  if errorlevel 1 set "ERR=1"
)

if "%ERR%"=="1" (
  echo [ERROR] Revert failed. A file is probably in use.
  echo         Close RadiKool and stop play/record, then run this again.
) else (
  echo Done. ffmpeg.exe / ffplay.exe restored to the real binaries.
)
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
