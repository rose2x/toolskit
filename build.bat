@echo off
setlocal
echo Building Tool Kit (self-contained, single file, win-x64)...
dotnet publish ToolkitApp.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -o .\build
if errorlevel 1 (
  echo.
  echo Build FAILED.
  pause
  exit /b 1
)
echo.
echo Done. Your EXE is: build\ToolKit.exe
pause
