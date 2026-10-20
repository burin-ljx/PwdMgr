<#
.SYNOPSIS
    用 Visual Studio 自带的 MSBuild 构建整个解决方案。

.DESCRIPTION
    WinUI 3 工程依赖 Visual Studio 的 Appx MSBuild 任务，dotnet CLI 无法构建，
    因此统一用 vswhere 找到 VS 的 MSBuild 再编译。

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Release
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw '找不到 vswhere.exe，请先安装 Visual Studio（含“使用 C++ 的桌面开发”或“.NET 桌面开发”工作负载）。'
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($msbuild)) {
    throw '找不到 Visual Studio 的 MSBuild.exe。'
}

Write-Host "使用 MSBuild：$msbuild"
& $msbuild "$PSScriptRoot\PwdMgr.sln" -restore -nologo -v:m "-p:Configuration=$Configuration"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "构建完成（$Configuration）。"
