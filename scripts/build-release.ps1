<#
.SYNOPSIS
    ElasticBreath 便携版打包脚本。

.DESCRIPTION
    执行 Release 构建、单文件发布，并将运行时所需资源（i18n、图标）与产物
    一起打包为版本号命名的 zip，便于分发。
    符合 design.md §9："纯便携版，不写注册表，不强制安装"。

.PARAMETER Version
    可选版本号。未指定时尝试从 csproj 读取 <Version>，再不行用日期。

.EXAMPLE
    .\scripts\build-release.ps1
    .\scripts\build-release.ps1 -Version 0.9.1
#>

[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# 解析版本号
if (-not $Version) {
    $csproj = Get-Content -Raw 'ElasticBreath.App\ElasticBreath.App.csproj'
    if ($csproj -match '<Version>([^<]+)</Version>') {
        $Version = $Matches[1].Trim()
    } else {
        $Version = Get-Date -Format 'yyyy-MM-dd'
    }
}
Write-Host "==> 构建版本: $Version" -ForegroundColor Cyan

# 清理 + 发布
Write-Host "==> 执行 dotnet publish (Portable profile)..." -ForegroundColor Cyan
dotnet publish ElasticBreath.App\ElasticBreath.App.csproj -c Release -p:PublishProfile=Portable --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败 (exit $LASTEXITCODE)"
}

$publishDir = 'ElasticBreath.App\bin\Release\net8.0-windows\publish'
if (-not (Test-Path $publishDir)) {
    throw "未找到发布目录: $publishDir"
}

# 打包
$artifactsDir = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
$zipName = "ElasticBreath-$Version-win-x64.zip"
$zipPath = Join-Path $artifactsDir $zipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "==> 打包到 $zipPath ..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "==> 完成: $zipPath ($sizeMb MB)" -ForegroundColor Green
Write-Host "    产物目录: $publishDir" -ForegroundColor Green
