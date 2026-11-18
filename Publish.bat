@echo off
title Ayaz Battery - Publish
echo ==========================================================
echo   Building Ayaz Battery Shop for copying to another PC...
echo ==========================================================
echo.

cd /d "%~dp0"

dotnet publish "BatteryShop\BatteryShop.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%~dp0Release\AyazBattery"

if errorlevel 1 (
    echo.
    echo  *** Publish FAILED. Scroll up to see the error. ***
    pause
    exit /b 1
)

copy /Y "%~dp0Install.bat" "%~dp0Release\AyazBattery\Install.bat" >nul

echo.
echo ==========================================================
echo   DONE!
echo   Copy this folder to the other PC (USB):
echo   %~dp0Release\AyazBattery
echo   On that PC, double-click Install.bat inside it.
echo   It installs to C:\AyazBattery and adds a Desktop shortcut.
echo ==========================================================
explorer "%~dp0Release\AyazBattery"
pause
