@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
  echo Compilador C# nao encontrado.
  echo Instale o .NET Framework 4.8 ^(Developer Pack ou o proprio Framework^).
  exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /debug- /win32icon:"%~dp0app.ico" /out:"%~dp0..\BackupFdbCliente.exe" /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs MainForm.cs Engine.cs
if errorlevel 1 exit /b 1
echo.
echo Compilado: %~dp0..\BackupFdbCliente.exe
exit /b 0
