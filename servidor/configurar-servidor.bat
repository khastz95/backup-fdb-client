@echo off
title Configurar servidor FTP de backup
cd /d "%~dp0"

net session >nul 2>&1
if %errorLevel% neq 0 (
  echo Solicitando administrador...
  powershell.exe -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File "%~dp0Configurar-ServidorFtp.ps1"
if errorlevel 1 (
  echo.
  echo Falhou. Veja a mensagem acima.
  pause
)
