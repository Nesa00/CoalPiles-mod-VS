@echo off
setlocal enabledelayedexpansion

echo === Building Coal Piles (Release) ===
dotnet build -c Release
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "(Get-Content modinfo.json -Raw | ConvertFrom-Json).name"`) do set NAME=%%A
for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "(Get-Content modinfo.json -Raw | ConvertFrom-Json).version"`) do set VERSION=%%A

set NAME=%NAME: =%

set STAGE=build_temp
set OUTDIR=.
set ZIPNAME=%NAME%_%VERSION%.zip

echo === Packaging %ZIPNAME% ===
if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%"
if not exist "%OUTDIR%" mkdir "%OUTDIR%"

set DLL_FOUND=0
for /r "bin\Release" %%F in (CoalPiles.dll) do (
    copy /y "%%F" "%STAGE%\" >nul
    set DLL_FOUND=1
)

if "%DLL_FOUND%"=="0" (
    echo ERROR: CoalPiles.dll not found in bin\Release
    rmdir /s /q "%STAGE%"
    pause
    exit /b 1
)

copy /y "modinfo.json" "%STAGE%\" >nul
if exist "modicon.png" copy /y "modicon.png" "%STAGE%\" >nul
if exist "assets" xcopy /e /i /y "assets" "%STAGE%\assets" >nul
if exist "%OUTDIR%\%ZIPNAME%" del /f /q "%OUTDIR%\%ZIPNAME%"
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%OUTDIR%\%ZIPNAME%' -Force"
rmdir /s /q "%STAGE%"

echo.
echo === Done! Created %OUTDIR%\%ZIPNAME% ===
pause