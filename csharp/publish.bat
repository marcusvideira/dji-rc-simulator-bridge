@echo off
echo ============================================
echo  DJI RC Simulator Bridge - Publish
echo ============================================
echo.

cd /d "%~dp0"
set OUT=publish

echo [1/2] Publishing Console app...
dotnet publish DjiRcSimBridge.Console\DjiRcSimBridge.Console.csproj -c Release -o %OUT%\console -v quiet
if %errorlevel% neq 0 (
    echo Console publish failed.
    pause
    exit /b 1
)

echo [2/2] Publishing GUI app...
dotnet publish DjiRcSimBridge.GUI\DjiRcSimBridge.GUI.csproj -c Release -o %OUT%\gui -v quiet
if %errorlevel% neq 0 (
    echo GUI publish failed.
    pause
    exit /b 1
)

echo.
echo ============================================
echo  Published successfully!
echo ============================================
echo.
echo Console: %OUT%\console\DjiRcSimBridge.exe
echo GUI:     %OUT%\gui\DjiRcSimBridgeGUI.exe
echo.
echo These are self-contained single-file executables.
echo Distribute the .exe + ViGEmClient.dll together.
echo.
pause
