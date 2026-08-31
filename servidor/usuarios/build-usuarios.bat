@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
  echo Compilador C# nao encontrado.
  echo Instale o .NET Framework 4.8.
  exit /b 1
)

if not exist "%~dp0..\lib\System.Data.SQLite.dll" (
  echo Falta servidor\lib\System.Data.SQLite.dll
  exit /b 1
)

set "OUT=%~dp0..\FtpUsers.exe"
"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ /debug- /out:"%OUT%" /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Data.dll /r:"%~dp0..\lib\System.Data.SQLite.dll" Program.cs UsersDb.cs UsuariosForm.cs
if errorlevel 1 exit /b 1

copy /Y "%~dp0..\lib\System.Data.SQLite.dll" "%~dp0..\System.Data.SQLite.dll" >nul
if not exist "%~dp0..\x64" mkdir "%~dp0..\x64"
if not exist "%~dp0..\x86" mkdir "%~dp0..\x86"
copy /Y "%~dp0..\lib\x64\SQLite.Interop.dll" "%~dp0..\x64\SQLite.Interop.dll" >nul
copy /Y "%~dp0..\lib\x86\SQLite.Interop.dll" "%~dp0..\x86\SQLite.Interop.dll" >nul

echo.
echo Compilado: %OUT%
"%OUT%" --init
if errorlevel 1 exit /b 1
exit /b 0
