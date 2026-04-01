@echo off
echo ============================================
echo  DJI RC Simulator Bridge - Build
echo ============================================
echo.

cd /d "%~dp0"
dotnet build DjiRcSimBridge.slnx -c Release -v quiet
if %errorlevel% neq 0 (
    echo Build failed.
    pause
    exit /b 1
)

echo Build succeeded.
echo.
echo Usage:
echo   run-console.bat [-p COM5]    Console mode with live debug output
echo   run-gui.bat [-p COM5]        GUI mode with system tray
echo.
