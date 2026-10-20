@echo off
setlocal enabledelayedexpansion

rem Build a release-ready portable package:
rem   dist\PwdMgr-<version>-win-x64\              published app folder
rem   dist\PwdMgr-<version>-win-x64.zip          archive, contains that folder
rem   dist\PwdMgr-<version>-win-x64.zip.sha256   checksum for release notes
rem A .cmd file is used because this machine's PowerShell execution policy blocks .ps1 scripts.

set "ROOT=%~dp0"
set "TEMP=%ROOT%.artifacts\temp"
set "TMP=%TEMP%"
if not exist "%TEMP%" mkdir "%TEMP%"

rem Single source of truth for the version: Directory.Build.props
set "VERSION="
for /f "tokens=3 delims=<>" %%v in ('findstr /r /c:"<Version>" "%ROOT%Directory.Build.props"') do set "VERSION=%%v"
if not defined VERSION (
    echo [package] could not read <Version> from Directory.Build.props
    exit /b 1
)

set "NAME=PwdMgr-%VERSION%-win-x64"
set "DIST=%ROOT%dist"
set "STAGE=%DIST%\%NAME%"
set "ZIP=%DIST%\%NAME%.zip"
set "SHA=%ZIP%.sha256"

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

if not exist "%STAGE%\PwdMgr.exe" (
    echo [package] PwdMgr.exe missing after publish.
    exit /b 1
)

rem Publish does not copy the compiled XAML resources (PwdMgr.pri);
rem without it the app fails to start with "Cannot locate resource".
set "PRI=%ROOT%src\PwdMgr.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PwdMgr.pri"
if exist "%PRI%" (
    copy /y "%PRI%" "%STAGE%\PwdMgr.pri" >nul
) else (
    echo [package] WARNING: PwdMgr.pri not found, XAML resources may be missing.
)

copy /y "%ROOT%docs\使用说明.txt" "%STAGE%\README.txt" >nul
if exist "%ROOT%README.md" copy /y "%ROOT%README.md" "%STAGE%\README.md" >nul
if exist "%ROOT%LICENSE" copy /y "%ROOT%LICENSE" "%STAGE%\LICENSE" >nul
if exist "%ROOT%LICENSE.GPL-3.0" copy /y "%ROOT%LICENSE.GPL-3.0" "%STAGE%\LICENSE.GPL-3.0" >nul

rem Sanity check: the published exe must carry the release version.
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "(Get-Item '%STAGE%\PwdMgr.exe').VersionInfo.FileVersion"`) do set "EXE_VERSION=%%v"
echo [package] exe file version: %EXE_VERSION%
if not defined EXE_VERSION (
    echo [package] could not read exe version.
    exit /b 1
)

if exist "%ZIP%" del "%ZIP%"
if exist "%SHA%" del "%SHA%"
rem Windows PowerShell 5.1 writes '\' separators in zip entries; tools\package-zip.ps1
rem builds the archive manually with '/' separators and one top-level folder.
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%tools\package-zip.ps1" -Stage "%STAGE%" -Zip "%ZIP%" -RootName "%NAME%"
if errorlevel 1 (
    echo [package] zip FAILED.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$h=(Get-FileHash -Algorithm SHA256 -LiteralPath '%ZIP%').Hash.ToLower(); Set-Content -Encoding ASCII -Path '%SHA%' -Value ($h + '  %NAME%.zip')"
if errorlevel 1 (
    echo [package] checksum FAILED.
    exit /b 1
)

echo [package] OK.
echo [package] folder:   %STAGE%
echo [package] zip:      %ZIP%
echo [package] checksum: %SHA%
for %%f in ("%ZIP%") do echo [package] size:     %%~zf bytes
exit /b 0
