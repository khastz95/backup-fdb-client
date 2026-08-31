@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
  echo Compilador C# nao encontrado.
  exit /b 1
)

set "ICO="
if exist "%~dp0..\app\app.ico" set "ICO=/win32icon:%~dp0..\app\app.ico"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /debug- %ICO% /out:"%~dp0PainelMonitor.exe" /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs PainelForm.cs FtpPainel.cs MontarBackup.cs
if errorlevel 1 exit /b 1
echo.
echo Compilado: %~dp0PainelMonitor.exe
exit /b 0
