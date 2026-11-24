@echo off
setlocal
cd /d "%~dp0"

dotnet build -c Release
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)
for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "(Get-Content modinfo.json -Raw | ConvertFrom-Json).version"`) do set "VERSION=%%A"

set "ZIPNAME=CoalPiles-%VERSION%.zip"

if exist "%ZIPNAME%" del "%ZIPNAME%"
powershell -NoProfile -Command "Compress-Archive -Path 'modinfo.json','modicon.png','assets','bin\Release\CoalPiles.dll' -DestinationPath '%ZIPNAME%' -Force"

del /Q "%AppData%\VintagestoryData\Mods\CoalPiles-*.zip" 2>nul
copy /Y "%ZIPNAME%" "%AppData%\VintagestoryData\Mods\"
start "" "D:\Vintagestory\Vintagestory.exe"