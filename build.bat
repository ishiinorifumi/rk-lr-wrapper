@echo off
setlocal
rem ffmpeg origin ラッパーをビルドする（.NET Framework の csc を使用）

set "HERE=%~dp0"
set "SRC=%HERE%ffmpeg_origin_wrapper.cs"
set "OUT=%HERE%ffmpeg.wrapper.exe"

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [エラー] csc.exe（.NET Framework）が見つかりません。
  exit /b 1
)

"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ /out:"%OUT%" "%SRC%"
if errorlevel 1 (
  echo [エラー] ビルドに失敗しました。
  exit /b 1
)

echo ビルド成功: %OUT%
endlocal
