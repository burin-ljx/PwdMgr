@echo off
setlocal enabledelayedexpansion

rem Build a portable (green) package:
rem   dist\PwdMgr\                      published app folder
rem   dist\PwdMgr-<version>-win-x64.zip packaged archive
rem A .cmd file is used because this machine's PowerShell execution policy blocks .ps1 scripts.

set "ROOT=%~dp0"
set "TEMP=%ROOT%.artifacts\temp"
set "TMP=%TEMP%"
if not exist "%TEMP%" mkdir "%TEMP%"

set "VERSION=1.0.0"
set "DIST=%ROOT%dist"
set "STAGE=%DIST%\PwdMgr"
set "ZIP=%DIST%\PwdMgr-%VERSION%-win-x64.zip"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [package] vswhere.exe not found. Install Visual Studio first.
    exit /b 1
)

set "MSBUILD="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
    if not defined MSBUILD set "MSBUILD=%%i"
)

if not defined MSBUILD (
    echo [package] MSBuild.exe not found.
    exit /b 1
)

echo [package] MSBuild: %MSBUILD%
echo [package] TEMP: %TEMP%
echo [package] version: %VERSION%

if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%"

"%MSBUILD%" "%ROOT%src\PwdMgr.App\PwdMgr.App.csproj" -restore -nologo -v:m -t:Publish ^
    -p:Configuration=Release -p:Platform=x64 -p:DebugType=None -p:DebugSymbols=false ^
    "-p:PublishDir=%STAGE%\"
if errorlevel 1 (
    echo [package] publish FAILED.
    exit /b 1
)

if not exist "%STAGE%\PwdMgr.App.exe" (
    echo [package] PwdMgr.App.exe missing after publish.
    exit /b 1
)

rem Publish does not copy the compiled XAML resources (PwdMgr.App.pri);
rem without it the app fails to start with "Cannot locate resource".
set "PRI=%ROOT%src\PwdMgr.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PwdMgr.App.pri"
if exist "%PRI%" (
    copy /y "%PRI%" "%STAGE%\PwdMgr.App.pri" >nul
) else (
    echo [package] WARNING: PwdMgr.App.pri not found, XAML resources may be missing.
)

copy /y "%ROOT%docs\使用说明.txt" "%STAGE%\README.txt" >nul
if exist "%ROOT%README.md" copy /y "%ROOT%README.md" "%STAGE%\README.md" >nul
if exist "%ROOT%LICENSE" copy /y "%ROOT%LICENSE" "%STAGE%\LICENSE" >nul
if exist "%ROOT%LICENSE.GPL-3.0" copy /y "%ROOT%LICENSE.GPL-3.0" "%STAGE%\LICENSE.GPL-3.0" >nul

if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%ZIP%' -CompressionLevel Optimal -Force"
if errorlevel 1 (
    echo [package] zip FAILED.
    exit /b 1
)

echo [package] OK.
echo [package] folder: %STAGE%
echo [package] zip:    %ZIP%
for %%f in ("%ZIP%") do echo [package] size:   %%~zf bytes
exit /b 0
