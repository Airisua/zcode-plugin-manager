param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $projectRoot "publish"

dotnet clean $projectRoot\PluginManager.csproj -c $Configuration --nologo
dotnet publish $projectRoot\PluginManager.csproj -c $Configuration --nologo

if (-not (Test-Path (Join-Path $outputDir "ZCodePluginManager.exe")))
{
    throw "发布完成但未找到 ZCodePluginManager.exe。"
}

Get-Item (Join-Path $outputDir "ZCodePluginManager.exe") | Select-Object FullName, Length, LastWriteTime
