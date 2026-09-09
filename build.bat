@echo off
setlocal
cd /d "%~dp0"

echo === Cliente ===
call "%~dp0app\build-cliente.bat"
if errorlevel 1 exit /b 1

echo.
echo === Painel ===
call "%~dp0painel-monitor\build-painel.bat"
if errorlevel 1 exit /b 1

echo.
echo === Usuarios FTP ===
call "%~dp0servidor\usuarios\build-usuarios.bat"
if errorlevel 1 exit /b 1

echo.
echo Tudo compilado.
exit /b 0
