@echo off
setlocal

cd /d "%~dp0"

:LOOP
echo [%date% %time%] Starting IngressLoadTest.exe >> log.txt

IngressLoadTest.exe

set EXIT_CODE=%ERRORLEVEL%

echo [%date% %time%] IngressLoadTest.exe exited. ExitCode=%EXIT_CODE% >> log.txt
echo [%date% %time%] Restarting after 5 seconds... >> log.txt

timeout /t 5 /nobreak >nul

goto LOOP
