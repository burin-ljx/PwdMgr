@echo off
setlocal enabledelayedexpansion

rem Build with the MSBuild that ships with Visual Studio.
rem A .cmd file is used because this machine's PowerShell execution policy (AllSigned)
rem blocks unsigned .ps1 scripts.

set "ROOT=%~dp0"
set "TEMP=%ROOT%.artifacts\temp"
set "TMP=%ROOT%.artifacts\temp"
if not exist "%TEMP%" mkdir "%TEMP%"

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
exit /b 0
