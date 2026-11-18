@echo off
title Ayaz Battery - Install
set TARGET=C:\AyazBattery

echo ==========================================================
echo   Installing Ayaz Battery Shop to %TARGET%
echo ==========================================================
echo.

tasklist /FI "IMAGENAME eq AyazBatteryShop.exe" | find /I "AyazBatteryShop.exe" >nul
if not errorlevel 1 (
    echo  Please close Ayaz Battery Shop first, then run this again.
    pause
    exit /b 1
)

if not exist "%TARGET%" mkdir "%TARGET%"
xcopy "%~dp0*" "%TARGET%\" /E /Y /I /Q >nul
if errorlevel 1 (
    echo  *** Copy FAILED. Try right-click Install.bat - Run as administrator. ***
    pause
    exit /b 1
)

powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop')+'\Ayaz Battery Shop.lnk'); $s.TargetPath='%TARGET%\AyazBatteryShop.exe'; $s.WorkingDirectory='%TARGET%'; $s.Save()"

echo.
echo  DONE! Installed to %TARGET%
echo  A shortcut "Ayaz Battery Shop" is on the Desktop.
echo  (Your shop data is kept separately and is not affected.)
echo.
pause
