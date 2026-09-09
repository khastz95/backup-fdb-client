#Requires -Version 5.1
<#
.SYNOPSIS
  Baixa o projeto backup-fdb-client (codigo + ultimos .exe do Release).

.EXAMPLE
  irm https://raw.githubusercontent.com/khastz95/backup-fdb-client/main/install.ps1 | iex

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/khastz95/backup-fdb-client/main/install.ps1 | iex"
#>
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Repo = 'khastz95/backup-fdb-client'
$Dest = Join-Path (Get-Location) 'backup-fdb-client'
$Ua = 'backup-fdb-client-install'

function Get-GitHubJson([string]$Url) {
    Invoke-RestMethod -Uri $Url -Headers @{ 'User-Agent' = $Ua; Accept = 'application/vnd.github+json' }
}

function Save-Url([string]$Url, [string]$OutFile) {
    $dir = Split-Path -Parent $OutFile
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing -Headers @{ 'User-Agent' = $Ua }
}

Write-Host ''
Write-Host '  Backup Firebird 3.0 + FTP'
Write-Host '  Baixando codigo e programas...'
Write-Host ''

if (Test-Path -LiteralPath (Join-Path $Dest '.git')) {
    Write-Host ("Atualizando git em {0}" -f $Dest)
    git -C $Dest pull --ff-only
}
elseif (Get-Command git -ErrorAction SilentlyContinue) {
    Write-Host ("git clone https://github.com/{0}.git" -f $Repo)
    git clone --depth 1 "https://github.com/$Repo.git" $Dest
}
else {
    Write-Host 'Git nao encontrado. Baixando ZIP da branch main...'
    $zip = Join-Path $env:TEMP 'backup-fdb-client-main.zip'
    Save-Url "https://github.com/$Repo/archive/refs/heads/main.zip" $zip
    $unpack = Join-Path $env:TEMP 'backup-fdb-client-unpack'
    if (Test-Path -LiteralPath $unpack) { Remove-Item -LiteralPath $unpack -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $unpack -Force
    $inner = Get-ChildItem -LiteralPath $unpack -Directory | Select-Object -First 1
    if (-not $inner) { throw 'ZIP do GitHub veio vazio.' }
    if (Test-Path -LiteralPath $Dest) { Remove-Item -LiteralPath $Dest -Recurse -Force }
    Move-Item -LiteralPath $inner.FullName -Destination $Dest
}

try {
    $rel = Get-GitHubJson "https://api.github.com/repos/$Repo/releases/latest"
    Write-Host ("Release {0}" -f $rel.tag_name)
    foreach ($asset in @($rel.assets)) {
        $name = [string]$asset.name
        if ($name -notmatch '\.(exe|example)$') { continue }
        $target = Join-Path $Dest $name
        if ($name -eq 'PainelMonitor.exe' -or $name -eq 'ftp-nativo.ini.example') {
            $target = Join-Path (Join-Path $Dest 'painel-monitor') $name
        }
        Write-Host ("  {0}" -f $name)
        Save-Url $asset.browser_download_url $target
    }
}
catch {
    Write-Host ('Nao baixou o Release (compile com build.bat): {0}' -f $_.Exception.Message)
}

$copies = @(
    @{ Src = 'config.ini.example'; Dst = 'config.ini' }
    @{ Src = (Join-Path 'servidor' 'servidor-ftp.ini.example'); Dst = (Join-Path 'servidor' 'servidor-ftp.ini') }
    @{ Src = (Join-Path 'painel-monitor' 'ftp-nativo.ini.example'); Dst = (Join-Path 'painel-monitor' 'ftp-nativo.ini') }
)
foreach ($c in $copies) {
    $from = Join-Path $Dest $c.Src
    $to = Join-Path $Dest $c.Dst
    if ((Test-Path -LiteralPath $from) -and -not (Test-Path -LiteralPath $to)) {
        Copy-Item -LiteralPath $from -Destination $to
        Write-Host ("Criado {0} (preencha; e so um modelo)." -f $c.Dst)
    }
}

Write-Host ''
Write-Host ("Pronto: {0}" -f $Dest)
Write-Host 'Passo a passo: docs\configuracao.md'
Write-Host '  - So neste PC: escolha "Somente neste computador" no cliente.'
Write-Host '  - Com servidor: monte o FTP (porta 9099) e depois o envio no cliente.'
Write-Host ''
