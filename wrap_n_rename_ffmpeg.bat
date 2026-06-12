@echo off
setlocal
rem RadiKool の libs フォルダに ffmpeg.wrapper.exe と一緒に置いて実行する。
rem   1) 本物 ffmpeg.exe → ffmpeg.origin.exe にリネーム（退避）
rem   2) ffmpeg.wrapper.exe → ffmpeg.exe にリネーム（ラッパー有効化）
rem 書き込みに失敗する場合は、この bat を右クリック→「管理者として実行」。

cd /d "%~dp0"

tasklist /FI "IMAGENAME eq Radikool.exe" 2>nul | find /I "Radikool.exe" >nul
if not errorlevel 1 (
  echo [エラー] RadiKool が起動中です。終了してから再実行してください。
  pause
  exit /b 1
)

if not exist "ffmpeg.wrapper.exe" (
  echo [エラー] ffmpeg.wrapper.exe が見つかりません。RadiKool の libs フォルダに一緒に置いてください。
  pause
  exit /b 1
)
if exist "ffmpeg.origin.exe" (
  echo [エラー] 既に ffmpeg.origin.exe があります。導入済みの可能性があります。
  pause
  exit /b 1
)
if not exist "ffmpeg.exe" (
  echo [エラー] 本物の ffmpeg.exe が見つかりません。RadiKool の libs フォルダで実行してください。
  pause
  exit /b 1
)

ren "ffmpeg.exe" "ffmpeg.origin.exe"
if errorlevel 1 (
  echo [エラー] 本物のリネームに失敗しました。管理者として実行してください。
  pause
  exit /b 1
)

ren "ffmpeg.wrapper.exe" "ffmpeg.exe"
if errorlevel 1 (
  echo [エラー] ラッパーのリネームに失敗しました。
  pause
  exit /b 1
)

echo 完了しました。
echo   本物    : ffmpeg.origin.exe （退避）
echo   ラッパー: ffmpeg.exe        （有効化）
echo RadiKool を起動して LR 局を録音できます。
pause
endlocal
