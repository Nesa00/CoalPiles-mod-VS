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
for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "(Get-Content modinfo.json -Raw | ConvertFrom-Json).name"`) do set "NAME=%%A"

set "NAME=%NAME: =-%"
set "ZIPNAME=%NAME%-%VERSION%.zip"


set "ZIPNAME=CoalPiles-%VERSION%.zip"

if exist "%ZIPNAME%" del "%ZIPNAME%"

copy /Y "bin\Release\CoalPiles.dll" "CoalPiles.dll" >nul
7z a -tzip "%ZIPNAME%" modinfo.json modicon.png assets CoalPiles.dll
del "CoalPiles.dll"

del /Q "%AppData%\VintagestoryData\Mods\%NAME%-*.zip" 2>nul
copy /Y "%ZIPNAME%" "%AppData%\VintagestoryData\Mods\"
start "" "D:\Vintagestory\Vintagestory.exe"