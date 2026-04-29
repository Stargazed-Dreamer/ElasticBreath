@echo off
REM 这个脚本会编译Release版本，适合分发给用户
setlocal

cd /d "%~dp0"
echo 正在编译Release版本...
echo.

echo 步骤1: 清理之前的编译结果...
dotnet clean ElasticBreath.sln -c Release -v minimal
if errorlevel 1 (
    echo 清理失败!
    pause
    exit /b 1
)

echo.
echo 步骤2: 编译Release版本...
dotnet build ElasticBreath.sln -c Release -v minimal
if errorlevel 1 (
    echo 编译失败!
    pause
    exit /b 1
)
pause