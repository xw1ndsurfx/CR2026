@echo off
setlocal
cd /d "%~dp0.."

dotnet publish .\Intersect.Client\Intersect.Client.csproj -p:Configuration=Release -p:PackageVersion=0.8.0-beta -p:Version=0.8.0 -p:INTERSECT_STEAMWORKS=true -r win-x64
if errorlevel 1 exit /b %errorlevel%

powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\prepare-steam-runtime.ps1"
if errorlevel 1 exit /b %errorlevel%

echo.
echo Steam client build completed successfully.
echo Output: Intersect.Client\bin\Release\net8.0\win-x64\publish
endlocal
