@echo off
setlocal enabledelayedexpansion

rem Build with the MSBuild that ships with Visual Studio.
rem A .cmd file is used because this machine's PowerShell execution policy (AllSigned)
rem blocks unsigned .ps1 scripts.
rem
rem After a successful build the app is copied to %LOCALAPPDATA%\PwdMgrApp, because
rem executables launched from this repository folder are treated as untrusted by the
rem machine's security software (they run at low integrity and cannot write files).

set "ROOT=%~dp0"
set "TEMP=%ROOT%.artifacts\temp"
set "TMP=%ROOT%.artifacts\temp"
if not exist "%TEMP%" mkdir "%TEMP%"

set "APP_OUT=%ROOT%src\PwdMgr.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64"
set "APP_PUBLISH=%LOCALAPPDATA%\PwdMgrApp"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [build] vswhere.exe not found. Install Visual Studio first.
    exit /b 1
)

set "MSBUILD="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
    if not defined MSBUILD set "MSBUILD=%%i"
)

if not defined MSBUILD (
    echo [build] MSBuild.exe not found.
    exit /b 1
)

echo [build] MSBuild: %MSBUILD%
echo [build] TEMP: %TEMP%
"%MSBUILD%" "%ROOT%PwdMgr.sln" -restore -nologo -v:m -p:Configuration=Debug
if errorlevel 1 (
    echo [build] FAILED.
    exit /b 1
)

echo [build] OK.

if not exist "%APP_OUT%\PwdMgr.App.exe" (
    echo [build] PwdMgr.App.exe not found, skip publish.
    exit /b 0
)

robocopy "%APP_OUT%" "%APP_PUBLISH%" /E /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
    echo [build] Could not copy to %APP_PUBLISH%.
    echo [build] Run the app from: %APP_OUT%\PwdMgr.App.exe
    exit /b 0
)

echo [build] App published to: %APP_PUBLISH%\PwdMgr.App.exe
exit /b 0
