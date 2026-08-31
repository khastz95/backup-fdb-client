@echo off
title Servidor FTP Backup Firebird - DEIXE ESTA JANELA ABERTA
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Servidor-FtpBackup.ps1"
echo.
echo Servidor FTP parou.
pause
