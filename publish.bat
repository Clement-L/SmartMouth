@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PROJECT_PATH=%SCRIPT_DIR%SmartMouth.App\SmartMouth.App.csproj"
set "OUTPUT_DIR=%SCRIPT_DIR%publish\win-x64"

dotnet publish "%PROJECT_PATH%" ^
  -c Release ^
  -f net10.0-windows ^
  -r win-x64 ^
  --self-contained false ^
  -o "%OUTPUT_DIR%"

if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

echo Published to: %OUTPUT_DIR%
endlocal
