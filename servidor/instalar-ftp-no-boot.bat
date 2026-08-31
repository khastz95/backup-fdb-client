@echo off
title Instalar FTP automatico no boot
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 (
  echo Solicitando administrador...
  powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar-FtpAutomatico.ps1"
if errorlevel 1 (
  echo.
  echo Falhou. Veja a mensagem acima.
)
pause
