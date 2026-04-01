@echo off
cd /d "%~dp0"
dotnet build DjiRcSimBridge.Console\DjiRcSimBridge.Console.csproj -c Release -v quiet
if %errorlevel% neq 0 (
    echo Build failed.
    pause
    exit /b 1
)
dotnet run --project DjiRcSimBridge.Console\DjiRcSimBridge.Console.csproj -c Release -- %*
