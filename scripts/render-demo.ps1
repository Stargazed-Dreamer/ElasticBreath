<#
.SYNOPSIS
    ElasticBreath 展示用截图生成脚本。

.DESCRIPTION
    调用 ElasticBreath.DemoRenderer 离线生成 6 种状态 PNG、顶部进度条五联对比 PNG、
    以及 Warning 脉冲 GIF。复用与实机 Layered Window 相同的渲染核心，像素级一致。
    背景默认为合成渐变壁纸；可通过 -Bg 指定自定义背景图测试不同壁纸下的效果。
    自定义背景的输出文件名会追加 --<basename> 后缀，不会覆盖默认版本，便于多图对比。

.PARAMETER Bg
    可选。自定义背景图路径（jpg/png/bmp 等）。未指定时用合成渐变壁纸。

.PARAMETER Out
    可选。输出目录。默认 docs/screenshots/。

.PARAMETER NoBuild
    跳过 Release 构建，直接用已编译产物运行。首次运行或代码改动后不要使用。

.EXAMPLE
    .\scripts\render-demo.ps1
    # 用合成渐变壁纸生成默认截图

.EXAMPLE
    .\scripts\render-demo.ps1 -Bg D:\wallpapers\1.jpg
    # 用自定义背景图生成，输出文件名带 --1 后缀

.EXAMPLE
    .\scripts\render-demo.ps1 -Bg "D:\my photo.jpg" -Out D:\out
    # 文件名带空格的背景图 + 自定义输出目录

.EXAMPLE
    Get-ChildItem D:\bg\*.jpg | ForEach-Object { .\scripts\render-demo.ps1 -Bg $_.FullName -NoBuild }
    # 批量测试多张背景图（首次请去掉 -NoBuild 以构建一次）
#>

[CmdletBinding()]
param(
    [string]$Bg,
    [string]$Out,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot 'ElasticBreath.DemoRenderer\ElasticBreath.DemoRenderer.csproj'

# 构造传给 DemoRenderer 的参数
$runArgs = @()
if ($Bg) {
    if (-not (Test-Path -LiteralPath $Bg)) {
        throw "背景图不存在: $Bg"
    }
    $bgFull = (Resolve-Path -LiteralPath $Bg).Path
    $runArgs += '--bg'
    $runArgs += $bgFull
}
if ($Out) {
    $runArgs += '--out'
    $runArgs += $Out
}

# 构建（除非 -NoBuild）
if (-not $NoBuild) {
    Write-Host "==> 构建 DemoRenderer (Release)..." -ForegroundColor Cyan
    dotnet build $proj -c Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败 (exit $LASTEXITCODE)"
    }
}

# 运行
Write-Host "==> 运行 DemoRenderer..." -ForegroundColor Cyan
if ($runArgs.Count -gt 0) {
    dotnet run --project $proj -c Release --no-build -- @runArgs
} else {
    dotnet run --project $proj -c Release --no-build
}
if ($LASTEXITCODE -ne 0) {
    throw "渲染失败 (exit $LASTEXITCODE)"
}

Write-Host "==> 完成" -ForegroundColor Green
