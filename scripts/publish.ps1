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

$zipPath = Join-Path $outputDir "ZCodePluginManager.zip"
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $outputDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$checksum = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $outputDir "SHA256SUMS") -Value "$checksum  ZCodePluginManager.zip"

Get-Item (Join-Path $outputDir "ZCodePluginManager.exe") | Select-Object FullName, Length, LastWriteTime
Get-Item $zipPath | Select-Object FullName, Length, LastWriteTime
