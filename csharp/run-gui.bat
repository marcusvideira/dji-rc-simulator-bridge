@echo off
cd /d "%~dp0"
dotnet build DjiRcSimBridge.GUI\DjiRcSimBridge.GUI.csproj -c Release -v quiet
if %errorlevel% neq 0 (
    echo Build failed.
    pause
    exit /b 1
)
start "" dotnet run --project DjiRcSimBridge.GUI\DjiRcSimBridge.GUI.csproj -c Release -- %*
