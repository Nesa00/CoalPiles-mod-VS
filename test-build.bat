@echo off
setlocal
cd /d "%~dp0"

dotnet build -c Release
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)
if exist "CoalPiles-1.0.0.zip" del "CoalPiles-1.0.0.zip"
powershell -NoProfile -Command "Compress-Archive -Path 'modinfo.json','assets','bin\Release\CoalPiles.dll' -DestinationPath 'CoalPiles-1.0.0.zip' -Force"
copy /Y "CoalPiles-1.0.0.zip" "%AppData%\VintagestoryData\Mods\"
start "" "D:\Vintagestory\Vintagestory.exe"