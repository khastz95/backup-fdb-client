@echo off
cd /d "%~dp0"
if not exist "%~dp0painel-monitor\PainelMonitor.exe" (
  echo Compilando o painel...
  call "%~dp0painel-monitor\build-painel.bat"
)
start "" "%~dp0painel-monitor\PainelMonitor.exe"
