#Requires -Version 5.1
#Requires -RunAsAdministrator
param(
    [switch]$Remover
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$taskName = 'Servidor FTP Backup Firebird'
$script = Join-Path $PSScriptRoot 'Servidor-FtpBackup.ps1'

if ($Remover) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Tarefa '$taskName' removida." -ForegroundColor Yellow
    return
}

if (-not (Test-Path -LiteralPath $script)) {
    throw "Nao achei $script"
}

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

$arg = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}"' -f $script
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -StartWhenAvailable

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $taskName

Write-Host ''
Write-Host "Tarefa '$taskName' criada e iniciada." -ForegroundColor Green
Write-Host 'O FTP sobe sozinho quando o Windows ligar.' -ForegroundColor Green
Write-Host 'Nao precisa deixar o Indy ligado. Pode fecha-lo.' -ForegroundColor Yellow
Write-Host ''
