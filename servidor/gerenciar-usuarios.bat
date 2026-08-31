@echo off
cd /d "%~dp0"
if not exist "%~dp0FtpUsers.exe" (
  echo Compile primeiro: usuarios\build-usuarios.bat
  pause
  exit /b 1
)
start "" "%~dp0FtpUsers.exe"
