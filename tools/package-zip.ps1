param(
    [Parameter(Mandatory = $true)][string]$Stage,
    [Parameter(Mandatory = $true)][string]$Zip,
    [Parameter(Mandatory = $true)][string]$RootName
)

# Windows PowerShell 5.1 的 ZipFile.CreateFromDirectory 会把条目名写成反斜杠分隔，
# 别的平台解压会出问题；这里手工建条目，统一用 '/' 并保留一个顶层目录。

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$stageFull = (Resolve-Path -LiteralPath $Stage).Path.TrimEnd('\')
$zipFull = [System.IO.Path]::GetFullPath($Zip)

if (Test-Path -LiteralPath $zipFull) {
    Remove-Item -LiteralPath $zipFull -Force
}

$prefix = $RootName.TrimEnd('/') + '/'
$archive = [System.IO.Compression.ZipFile]::Open($zipFull, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $files = Get-ChildItem -LiteralPath $stageFull -Recurse -File | Sort-Object FullName
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($stageFull.Length + 1).Replace('\', '/')
        $entry = $archive.CreateEntry($prefix + $relative, [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $file.LastWriteTime

        $source = [System.IO.File]::OpenRead($file.FullName)
        try {
            $target = $entry.Open()
            try {
                $source.CopyTo($target)
            }
            finally {
                $target.Dispose()
            }
        }
        finally {
            $source.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}

Write-Output "zip entries: $($files.Count)"
