@echo off
setlocal

cd /d "%~dp0"

dotnet publish IngressLoadTest.csproj ^
  -c Release ^
  --self-contained false ^
  -o publish\portable

if errorlevel 1 exit /b %errorlevel%

echo.
echo Portable publish created:
echo   %CD%\publish\portable
echo.
echo Run on Windows:
echo   dotnet IngressLoadTest.dll
echo.
echo Run on Ubuntu:
echo   dotnet IngressLoadTest.dll
