#Requires -Version 5.1
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'servidor-ftp.ini')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

function Get-IniValue {
    param($Path, $Section, $Key, $Default = '')
    $section = $null
    foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
        $t = $line.Trim()
        if ($t.StartsWith(';') -or $t.Length -eq 0) { continue }
        if ($t -match '^\[(.+)\]$') { $section = $Matches[1]; continue }
        if ($section -eq $Section -and $t.IndexOf('=') -gt 0) {
            $k = $t.Substring(0, $t.IndexOf('=')).Trim()
            if ($k -eq $Key) { return $t.Substring($t.IndexOf('=') + 1).Trim() }
        }
    }
    return $Default
}

$script:SrvDir = $PSScriptRoot

function Write-SrvLog([string]$Message) {
    $line = '[{0}] {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    Write-Host $line
    $base = $script:SrvDir
    if ([string]::IsNullOrWhiteSpace($base)) { $base = $SrvDir }
    if ([string]::IsNullOrWhiteSpace($base)) { $base = $PSScriptRoot }
    $logDir = Join-Path $base 'logs'
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    Add-Content -LiteralPath (Join-Path $logDir 'ftp-servidor.log') -Value $line -Encoding UTF8
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config nao encontrada: $ConfigPath"
}

$root = Get-IniValue $ConfigPath 'Servidor' 'Pasta' 'E:\Backup2'
$port = [int](Get-IniValue $ConfigPath 'Servidor' 'Porta' '9099')
$pasvFrom = [int](Get-IniValue $ConfigPath 'Servidor' 'PassivaInicio' '50200')
$pasvTo = [int](Get-IniValue $ConfigPath 'Servidor' 'PassivaFim' '50300')
$publicIp = Get-IniValue $ConfigPath 'Servidor' 'IpPublico' '127.0.0.1'
$user = Get-IniValue $ConfigPath 'Usuario' 'Login' 'backupclientes'
$pass = Get-IniValue $ConfigPath 'Usuario' 'Senha' ''
$retencaoCompletos = [int](Get-IniValue $ConfigPath 'Retencao' 'Completos' '1')
$retencaoIncrementais = [int](Get-IniValue $ConfigPath 'Retencao' 'Incrementais' '6')
if ($retencaoCompletos -lt 1) { $retencaoCompletos = 1 }
if ($retencaoIncrementais -lt 1) { $retencaoIncrementais = 6 }

if (-not (Test-Path -LiteralPath $root)) {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
}
$root = [IO.Path]::GetFullPath($root)
$ftpUsersExe = Join-Path $script:SrvDir 'FtpUsers.exe'
$ftpUsersDb = Join-Path $script:SrvDir 'sqlite.db'

function Test-FtpDownloadUser {
    param([string]$Email, [string]$Password)
    $base = $script:SrvDir
    if ([string]::IsNullOrWhiteSpace($base)) { $base = $SrvDir }
    $exe = Join-Path $base 'FtpUsers.exe'
    if (-not (Test-Path -LiteralPath $exe)) { return $null }
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $exe
        $psi.Arguments = '--check'
        $psi.WorkingDirectory = $base
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
        $p = New-Object System.Diagnostics.Process
        $p.StartInfo = $psi
        [void]$p.Start()
        $utf8 = New-Object System.Text.UTF8Encoding $false
        $sw = New-Object System.IO.StreamWriter($p.StandardInput.BaseStream, $utf8)
        $sw.NewLine = "`n"
        $sw.WriteLine($Email)
        $sw.WriteLine($Password)
        $sw.Flush()
        $sw.Close()
        if (-not $p.WaitForExit(8000)) {
            try { $p.Kill() } catch { }
            return $null
        }
        $out = $p.StandardOutput.ReadToEnd().Trim()
        if ($p.ExitCode -eq 0 -and $out.StartsWith('OK')) {
            $role = $out.Substring(2).Trim()
            if ([string]::IsNullOrWhiteSpace($role)) { $role = 'download' }
            return $role.ToLowerInvariant()
        }
    }
    catch {
        Write-SrvLog ("Auth sqlite: {0}" -f $_.Exception.Message)
    }
    return $null
}

function Test-FtpPodeEnviar($state) {
    return $state.Role -eq 'upload'
}

function Test-FtpPodeBaixar($state) {
    return $state.Role -eq 'download' -or $state.Role -eq 'admin'
}

if (Test-Path -LiteralPath $ftpUsersExe) {
    try {
        $init = Start-Process -FilePath $ftpUsersExe -ArgumentList '--init' -WorkingDirectory $script:SrvDir -Wait -PassThru -WindowStyle Hidden
        if ($init.ExitCode -eq 0) { Write-SrvLog ("sqlite.db pronto: {0}" -f $ftpUsersDb) }
        else { Write-SrvLog ("sqlite.db init falhou (codigo {0})" -f $init.ExitCode) }
    }
    catch {
        Write-SrvLog ("sqlite.db: {0}" -f $_.Exception.Message)
    }
}
else {
    Write-SrvLog 'FtpUsers.exe ausente. Cadastre usuarios com usuarios\build-usuarios.bat'
}

function ConvertFrom-VirtualPath {
    param([string]$Virtual, [string]$Current)
    $v = $Virtual.Replace('\', '/')
    if (-not $v.StartsWith('/')) {
        $cur = $Current.TrimEnd('/')
        if ($cur -eq '') { $cur = '/' }
        if ($cur -eq '/') { $v = "/$v" } else { $v = "$cur/$v" }
    }
    $parts = New-Object System.Collections.Generic.List[string]
    foreach ($p in $v.Split(@('/'), [StringSplitOptions]::RemoveEmptyEntries)) {
        if ($p -eq '.') { continue }
        if ($p -eq '..') {
            if ($parts.Count -gt 0) { $parts.RemoveAt($parts.Count - 1) }
            continue
        }
        $parts.Add($p)
    }
    if ($parts.Count -eq 0) { return '/' }
    return '/' + ($parts -join '/')
}

function ConvertTo-RealPath {
    param([string]$Virtual)
    $rel = $Virtual.Trim('/').Replace('/', '\')
    if ([string]::IsNullOrWhiteSpace($rel)) { return $root }
    $full = [IO.Path]::GetFullPath((Join-Path $root $rel))
    $rootSlash = $root.TrimEnd('\') + '\'
    if ($full.Equals($root, [StringComparison]::OrdinalIgnoreCase)) { return $full }
    if (-not $full.StartsWith($rootSlash, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Caminho fora da pasta de backup'
    }
    return $full
}

function Get-VirtualFromReal {
    param([string]$Real)
    $full = [IO.Path]::GetFullPath($Real)
    if ($full.Equals($root, [StringComparison]::OrdinalIgnoreCase)) { return '/' }
    $rootSlash = $root.TrimEnd('\') + '\'
    if (-not $full.StartsWith($rootSlash, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Caminho fora da pasta de backup'
    }
    return '/' + $full.Substring($rootSlash.Length).Replace('\', '/')
}

function Write-XtreeDir {
    param($Writer, [string]$Dir)
    Get-ChildItem -LiteralPath $Dir -Force -ErrorAction SilentlyContinue | ForEach-Object {
        $virt = Get-VirtualFromReal $_.FullName
        $kind = 'F'
        $size = [int64]0
        if ($_.PSIsContainer) { $kind = 'D' } else { $size = [int64]$_.Length }
        $when = $_.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
        $safe = $virt.Replace('|', '_')
        Send-Ftp $Writer ('211-{0}|{1}|{2}|{3}' -f $kind, $safe, $size, $when)
        if ($_.PSIsContainer) { Write-XtreeDir $Writer $_.FullName }
    }
}

function Test-PastaCiclo([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name)) { return $false }
    return $Name.StartsWith('Completo_', [StringComparison]::OrdinalIgnoreCase) -or
        $Name.StartsWith('cadeia_', [StringComparison]::OrdinalIgnoreCase)
}

function Invoke-RetencaoAposGravacao {
    param([string]$ArquivoReal)
    if ([string]::IsNullOrWhiteSpace($ArquivoReal) -or -not (Test-Path -LiteralPath $ArquivoReal)) { return }
    $cycleDir = [IO.Path]::GetDirectoryName($ArquivoReal)
    $cycleName = [IO.Path]::GetFileName($cycleDir)
    if (-not (Test-PastaCiclo $cycleName)) { return }
    $parent = [IO.Path]::GetDirectoryName($cycleDir)
    if ([string]::IsNullOrWhiteSpace($parent) -or -not (Test-Path -LiteralPath $parent)) { return }

    $safe = ($parent.ToLowerInvariant() -replace '[^a-z0-9]', '_')
    if ($safe.Length -gt 60) { $safe = $safe.Substring($safe.Length - 60) }
    $mtx = $null
    try {
        $mtx = New-Object Threading.Mutex($false, "Local\FtpBackupRetencao-$safe")
        if (-not $mtx.WaitOne(30000)) { return }
        $keepC = [Math]::Max(1, $retencaoCompletos)
        $keepI = [Math]::Max(1, $retencaoIncrementais)
        $cycles = @(Get-ChildItem -LiteralPath $parent -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-PastaCiclo $_.Name } |
            Sort-Object Name -Descending)
        if ($cycles.Count -gt $keepC) {
            $cycles | Select-Object -Skip $keepC | ForEach-Object {
                Write-SrvLog ("Retencao: pasta antiga removida {0}" -f $_.FullName)
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
            $cycles = $cycles | Select-Object -First $keepC
        }
        foreach ($c in $cycles) {
            $comps = @(Get-ChildItem -LiteralPath $c.FullName -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like 'Completo_*.nbk' -or $_.Name -like '*_L0.nbk' } |
                Sort-Object Name -Descending)
            if ($comps.Count -gt 1) {
                $comps | Select-Object -Skip 1 | ForEach-Object {
                    Write-SrvLog ("Retencao: completo extra removido {0}" -f $_.Name)
                    Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
                }
            }
            $incs = @(Get-ChildItem -LiteralPath $c.FullName -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like 'Incremental_*.nbk' -or $_.Name -like '*_L1.nbk' } |
                Sort-Object Name -Descending)
            if ($incs.Count -gt $keepI) {
                $incs | Select-Object -Skip $keepI | ForEach-Object {
                    Write-SrvLog ("Retencao: incremental antigo removido {0}" -f $_.Name)
                    Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
                }
            }
        }
        $restInc = @(Get-ChildItem -LiteralPath $parent -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'Incremental_*.nbk' }).Count
        $restComp = @(Get-ChildItem -LiteralPath $parent -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'Completo_*.nbk' }).Count
        Write-SrvLog ("Retencao {0}: {1} completo(s), {2} do dia (limite {3}+{4})" -f (Split-Path $parent -Leaf), $restComp, $restInc, $keepC, $keepI)
    }
    catch {
        Write-SrvLog ("Retencao falhou: {0}" -f $_.Exception.Message)
    }
    finally {
        if ($mtx) {
            try { $mtx.ReleaseMutex() } catch { }
            try { $mtx.Dispose() } catch { }
        }
    }
}

function Send-Ftp($Writer, [string]$Line) {
    $Writer.WriteLine($Line)
    Write-SrvLog "-> $Line"
}

function Close-Pasv($State) {
    if ($null -ne $State.PasvListener) {
        try { $State.PasvListener.Stop() } catch { }
        $State.PasvListener = $null
    }
}

function Open-PasvPort {
    param($State)
    Close-Pasv $State
    for ($p = $pasvFrom; $p -le $pasvTo; $p++) {
        try {
            $ep = New-Object System.Net.IPEndPoint([Net.IPAddress]::Any, $p)
            $l = New-Object System.Net.Sockets.TcpListener $ep
            $l.Start()
            $State.PasvListener = $l
            $State.PasvPort = $p
            return $p
        }
        catch {
            continue
        }
    }
    throw 'Nenhuma porta passiva livre'
}

function Get-PasvClient {
    param($State, [int]$TimeoutMs = 60000)
    if ($null -eq $State.PasvListener) { throw 'PASV nao iniciado' }
    $l = $State.PasvListener
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while (-not $l.Pending()) {
        if ($sw.ElapsedMilliseconds -gt $TimeoutMs) {
            Close-Pasv $State
            throw 'Timeout aguardando conexao de dados PASV'
        }
        Start-Sleep -Milliseconds 50
    }
    $c = $l.AcceptTcpClient()
    Close-Pasv $State
    return $c
}

function Handle-Client {
    param([Net.Sockets.TcpClient]$Client)

    $stream = $Client.GetStream()
    $stream.ReadTimeout = 90000
    $stream.WriteTimeout = 90000
    $Client.ReceiveTimeout = 90000
    $Client.SendTimeout = 90000
    $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::ASCII, $false, 1024, $true)
    $writer = New-Object IO.StreamWriter($stream, [Text.Encoding]::ASCII, 1024, $true)
    $writer.NewLine = "`r`n"
    $writer.AutoFlush = $true

    $state = @{
        User         = $null
        Auth         = $false
        Role         = $null
        Virtual      = '/'
        PasvListener = $null
        PasvPort     = 0
    }

    try {
            Send-Ftp $writer '220 Servidor FTP Backup Firebird pronto. XSTOR=1 XRETR=1'
        $keepSession = $true
        while ($keepSession -and $Client.Connected) {
            $line = $null
            try {
                $line = $reader.ReadLine()
            }
            catch {
                break
            }
            if ($null -eq $line) { break }
            if ($line.Length -ge 5 -and $line.Substring(0, 5).ToUpperInvariant() -eq 'PASS ') {
                Write-SrvLog '<- PASS ****'
            }
            else {
                Write-SrvLog "<- $line"
            }
            $cmd = $line
            $arg = ''
            $sp = $line.IndexOf(' ')
            if ($sp -gt 0) {
                $cmd = $line.Substring(0, $sp)
                $arg = $line.Substring($sp + 1)
            }
            $cmdU = $cmd.ToUpperInvariant().Trim()

            if ($cmdU -eq 'XSTOR') {
                if (-not $state.Auth) {
                    Send-Ftp $writer '530 Faca login.'
                    continue
                }
                if (-not (Test-FtpPodeEnviar $state)) {
                    Send-Ftp $writer '550 Sem permissao de envio.'
                    continue
                }
                $fs = $null
                try {
                    $raw = $arg.Trim()
                    $sp = $raw.LastIndexOf(' ')
                    if ($sp -le 0) { throw 'XSTOR precisa do nome e do tamanho' }
                    $name = $raw.Substring(0, $sp).Trim()
                    $size = [long]$raw.Substring($sp + 1).Trim()
                    if ($size -lt 0) { throw 'Tamanho invalido' }
                    $nv = ConvertFrom-VirtualPath $name $state.Virtual
                    $real = ConvertTo-RealPath $nv
                    $dir = Split-Path -Parent $real
                    if (-not (Test-Path -LiteralPath $dir)) {
                        New-Item -ItemType Directory -Path $dir -Force | Out-Null
                    }
                    Send-Ftp $writer '150 Opening BINARY mode data connection.'
                    $prevTimeout = $stream.ReadTimeout
                    $stream.ReadTimeout = 3600000
                    $fs = [IO.File]::Create($real)
                    $buf = New-Object byte[] 65536
                    $left = $size
                    while ($left -gt 0) {
                        $want = [int][Math]::Min($buf.Length, $left)
                        $n = $stream.Read($buf, 0, $want)
                        if ($n -le 0) { throw 'Conexao fechada no meio do arquivo' }
                        $fs.Write($buf, 0, $n)
                        $left -= $n
                    }
                    $fs.Close(); $fs = $null
                    $stream.ReadTimeout = $prevTimeout
                    Send-Ftp $writer '226 Transfer complete.'
                    Write-SrvLog ("Arquivo gravado: {0}" -f $real)
                    Invoke-RetencaoAposGravacao $real
                }
                catch {
                    if ($fs) { try { $fs.Close() } catch { } }
                    try { $stream.ReadTimeout = 90000 } catch { }
                    Send-Ftp $writer ('426 XSTOR falhou: {0}' -f $_.Exception.Message)
                }
                continue
            }

            if ($cmdU -eq 'XDIR') {
                if (-not $state.Auth) {
                    Send-Ftp $writer '530 Faca login.'
                    continue
                }
                try {
                    $real = ConvertTo-RealPath $state.Virtual
                    Send-Ftp $writer '211-Lista'
                    Get-ChildItem -LiteralPath $real -Force -ErrorAction SilentlyContinue | ForEach-Object {
                        Send-Ftp $writer ('211-{0}' -f $_.Name)
                    }
                    Send-Ftp $writer '211 Fim'
                }
                catch {
                    Send-Ftp $writer ('550 XDIR falhou: {0}' -f $_.Exception.Message)
                }
                continue
            }

            if ($cmdU -eq 'XTREE') {
                if (-not $state.Auth) {
                    Send-Ftp $writer '530 Faca login.'
                    continue
                }
                if (-not (Test-FtpPodeBaixar $state)) {
                    Send-Ftp $writer '550 Sem permissao de download. Use o e-mail cadastrado no sqlite.db.'
                    continue
                }
                try {
                    $start = ConvertTo-RealPath $state.Virtual
                    Send-Ftp $writer '211-Lista'
                    Write-XtreeDir $writer $start
                    Send-Ftp $writer '211 Fim'
                }
                catch {
                    Send-Ftp $writer ('550 XTREE falhou: {0}' -f $_.Exception.Message)
                }
                continue
            }

            if ($cmdU -eq 'XRETR') {
                if (-not $state.Auth) {
                    Send-Ftp $writer '530 Faca login.'
                    continue
                }
                if (-not (Test-FtpPodeBaixar $state)) {
                    Send-Ftp $writer '550 Sem permissao de download. Use o e-mail cadastrado no sqlite.db.'
                    continue
                }
                $fs = $null
                try {
                    $nv = ConvertFrom-VirtualPath $arg.Trim() $state.Virtual
                    $real = ConvertTo-RealPath $nv
                    if (-not (Test-Path -LiteralPath $real -PathType Leaf)) { throw 'Arquivo nao existe' }
                    $len = (Get-Item -LiteralPath $real).Length
                    Send-Ftp $writer ('150 XRETR {0}' -f $len)
                    $go = $reader.ReadLine()
                    if ($null -eq $go -or $go.Trim().ToUpperInvariant() -ne 'XRGO') { throw 'Cliente nao confirmou XRGO' }
                    $prevTimeout = $stream.WriteTimeout
                    $stream.WriteTimeout = 3600000
                    $fs = [IO.File]::OpenRead($real)
                    $buf = New-Object byte[] 65536
                    while (($n = $fs.Read($buf, 0, $buf.Length)) -gt 0) {
                        $stream.Write($buf, 0, $n)
                    }
                    $stream.Flush()
                    $fs.Close(); $fs = $null
                    $stream.WriteTimeout = $prevTimeout
                    Send-Ftp $writer '226 Transfer complete.'
                    Write-SrvLog ("Arquivo enviado: {0}" -f $real)
                }
                catch {
                    if ($fs) { try { $fs.Close() } catch { } }
                    try { $stream.WriteTimeout = 90000 } catch { }
                    Send-Ftp $writer ('426 XRETR falhou: {0}' -f $_.Exception.Message)
                }
                continue
            }

            switch -Exact ($cmdU) {
                'USER' {
                    $state.User = $arg.Trim()
                    Send-Ftp $writer '331 Senha.'
                }
                'PASS' {
                    if ($state.User -eq $user -and $arg -eq $pass) {
                        $state.Auth = $true
                        $state.Role = 'upload'
                        Send-Ftp $writer ('230 Login ok (envio). Pasta inicial {0}.' -f $root)
                    }
                    else {
                        $role = Test-FtpDownloadUser $state.User $arg
                        if ($role) {
                            $state.Auth = $true
                            $state.Role = $role
                            Send-Ftp $writer ('230 Login ok (download). Pasta inicial {0}.' -f $root)
                        }
                        else {
                            Send-Ftp $writer '530 Login invalido.'
                        }
                    }
                }
                'QUIT' { Send-Ftp $writer '221 Tchau.'; $keepSession = $false }
                'NOOP' { Send-Ftp $writer '200 OK' }
                'SYST' { Send-Ftp $writer '215 Windows_NT' }
                'FEAT' {
                    Send-Ftp $writer '211-Recursos'
                    Send-Ftp $writer ' SIZE'
                    Send-Ftp $writer ' PASV'
                    Send-Ftp $writer ' XSTOR'
                    Send-Ftp $writer ' XDIR'
                    Send-Ftp $writer ' XTREE'
                    Send-Ftp $writer ' XRETR'
                    Send-Ftp $writer '211 Fim'
                }
                'OPTS' { Send-Ftp $writer '200 OK' }
                'AUTH' { Send-Ftp $writer '504 FTP simples, sem TLS.' }
                'PORT' { Send-Ftp $writer '502 Use PASV.' }
                'TYPE' { Send-Ftp $writer '200 Tipo I' }
                'PWD' { Send-Ftp $writer ('257 "{0}"' -f $state.Virtual) }
                'XPWD' { Send-Ftp $writer ('257 "{0}"' -f $state.Virtual) }
                default {
                    if (-not $state.Auth -and $cmdU -notin @('USER', 'PASS', 'QUIT', 'NOOP')) {
                        Send-Ftp $writer '530 Faca login.'
                        continue
                    }
                    switch -Exact ($cmdU) {
                        'CWD' {
                            try {
                                $nv = ConvertFrom-VirtualPath $arg $state.Virtual
                                $real = ConvertTo-RealPath $nv
                                if (-not (Test-Path -LiteralPath $real -PathType Container)) {
                                    Send-Ftp $writer '550 Pasta nao existe.'
                                }
                                else {
                                    $state.Virtual = $nv
                                    Send-Ftp $writer '250 CWD Command successful.'
                                }
                            }
                            catch { Send-Ftp $writer '550 CWD falhou.' }
                        }
                        'CDUP' {
                            $state.Virtual = ConvertFrom-VirtualPath '..' $state.Virtual
                            Send-Ftp $writer '250 CDUP ok.'
                        }
                        'MKD' {
                            if (-not (Test-FtpPodeEnviar $state)) {
                                Send-Ftp $writer '550 Sem permissao de envio.'
                                continue
                            }
                            try {
                                $nv = ConvertFrom-VirtualPath $arg $state.Virtual
                                $real = ConvertTo-RealPath $nv
                                if (-not (Test-Path -LiteralPath $real)) {
                                    New-Item -ItemType Directory -Path $real -Force | Out-Null
                                }
                                Send-Ftp $writer ('257 "{0}" created' -f $nv)
                            }
                            catch { Send-Ftp $writer '550 MKD falhou.' }
                        }
                        'RMD' {
                            if (-not (Test-FtpPodeEnviar $state)) {
                                Send-Ftp $writer '550 Sem permissao de envio.'
                                continue
                            }
                            try {
                                $real = ConvertTo-RealPath (ConvertFrom-VirtualPath $arg $state.Virtual)
                                if (Test-Path -LiteralPath $real -PathType Container) {
                                    Remove-Item -LiteralPath $real -Recurse -Force
                                    Send-Ftp $writer '250 RMD ok.'
                                }
                                else { Send-Ftp $writer '550 Pasta nao existe.' }
                            }
                            catch { Send-Ftp $writer '550 RMD falhou.' }
                        }
                        'DELE' {
                            if (-not (Test-FtpPodeEnviar $state)) {
                                Send-Ftp $writer '550 Sem permissao de envio.'
                                continue
                            }
                            try {
                                $real = ConvertTo-RealPath (ConvertFrom-VirtualPath $arg $state.Virtual)
                                if (Test-Path -LiteralPath $real -PathType Leaf) {
                                    Remove-Item -LiteralPath $real -Force
                                    Send-Ftp $writer '250 DELE ok.'
                                }
                                else { Send-Ftp $writer '550 Arquivo nao existe.' }
                            }
                            catch { Send-Ftp $writer '550 DELE falhou.' }
                        }
                        'SIZE' {
                            try {
                                $real = ConvertTo-RealPath (ConvertFrom-VirtualPath $arg $state.Virtual)
                                if (Test-Path -LiteralPath $real -PathType Leaf) {
                                    Send-Ftp $writer ('213 {0}' -f (Get-Item -LiteralPath $real).Length)
                                }
                                else { Send-Ftp $writer '550 Sem arquivo.' }
                            }
                            catch { Send-Ftp $writer '550 SIZE falhou.' }
                        }
                        'PASV' {
                            try {
                                $p = Open-PasvPort $state
                                $oct = $publicIp.Split('.')
                                if ($oct.Count -ne 4) { throw 'IpPublico invalido no servidor-ftp.ini' }
                                $p1 = [int][Math]::Floor($p / 256)
                                $p2 = $p % 256
                                Send-Ftp $writer ('227 Entering Passive Mode ({0},{1},{2},{3},{4},{5}).' -f [int]$oct[0], [int]$oct[1], [int]$oct[2], [int]$oct[3], $p1, $p2)
                            }
                            catch {
                                Send-Ftp $writer ('425 PASV falhou: {0}' -f $_.Exception.Message)
                            }
                        }
                        'NLST' {
                            $data = $null
                            try {
                                $real = ConvertTo-RealPath $state.Virtual
                                Send-Ftp $writer '150 Opening ASCII mode data connection for file list.'
                                $data = Get-PasvClient $state
                                $ns = $data.GetStream()
                                $sw = New-Object IO.StreamWriter($ns, [Text.Encoding]::ASCII)
                                $sw.NewLine = "`r`n"
                                Get-ChildItem -LiteralPath $real -Force -ErrorAction SilentlyContinue | ForEach-Object {
                                    $sw.WriteLine($_.Name)
                                }
                                $sw.Flush(); $sw.Dispose()
                                $data.Close()
                                Send-Ftp $writer '226 Transfer complete.'
                            }
                            catch {
                                if ($data) { try { $data.Close() } catch { } }
                                Send-Ftp $writer ('426 NLST falhou: {0}' -f $_.Exception.Message)
                            }
                        }
                        'LIST' {
                            $data = $null
                            try {
                                $real = ConvertTo-RealPath $state.Virtual
                                Send-Ftp $writer '150 Opening ASCII mode data connection for file list.'
                                $data = Get-PasvClient $state
                                $ns = $data.GetStream()
                                $sw = New-Object IO.StreamWriter($ns, [Text.Encoding]::ASCII)
                                $sw.NewLine = "`r`n"
                                Get-ChildItem -LiteralPath $real -Force -ErrorAction SilentlyContinue | ForEach-Object {
                                    $sw.WriteLine($_.Name)
                                }
                                $sw.Flush(); $sw.Dispose()
                                $data.Close()
                                Send-Ftp $writer '226 Transfer complete.'
                            }
                            catch {
                                if ($data) { try { $data.Close() } catch { } }
                                Send-Ftp $writer ('426 LIST falhou: {0}' -f $_.Exception.Message)
                            }
                        }
                        'STOR' {
                            if (-not (Test-FtpPodeEnviar $state)) {
                                Send-Ftp $writer '550 Sem permissao de envio.'
                                continue
                            }
                            $data = $null
                            $fs = $null
                            try {
                                $nv = ConvertFrom-VirtualPath $arg $state.Virtual
                                $real = ConvertTo-RealPath $nv
                                $dir = Split-Path -Parent $real
                                if (-not (Test-Path -LiteralPath $dir)) {
                                    New-Item -ItemType Directory -Path $dir -Force | Out-Null
                                }
                                Send-Ftp $writer '150 Opening BINARY mode data connection.'
                                $data = Get-PasvClient $state
                                $ns = $data.GetStream()
                                $fs = [IO.File]::Create($real)
                                $buf = New-Object byte[] 65536
                                while (($n = $ns.Read($buf, 0, $buf.Length)) -gt 0) {
                                    $fs.Write($buf, 0, $n)
                                }
                                $fs.Close(); $fs = $null
                                $data.Close(); $data = $null
                                Send-Ftp $writer '226 Transfer complete.'
                                Write-SrvLog ("Arquivo gravado: {0}" -f $real)
                                Invoke-RetencaoAposGravacao $real
                            }
                            catch {
                                if ($fs) { try { $fs.Close() } catch { } }
                                if ($data) { try { $data.Close() } catch { } }
                                Send-Ftp $writer ('426 STOR falhou: {0}' -f $_.Exception.Message)
                            }
                        }
                        default { Send-Ftp $writer ('502 Comando nao suportado: {0}' -f $cmdU) }
                    }
                }
            }
        }
    }
    finally {
        Close-Pasv $state
        try { $reader.Dispose() } catch { }
        try { $writer.Dispose() } catch { }
        try { $Client.Close() } catch { }
    }
}

Write-Host ''
Write-Host '  +--------------------------------------------------+' -ForegroundColor Cyan
Write-Host '  |  SERVIDOR DE COPIAS FTP                          |' -ForegroundColor Cyan
Write-Host '  |  Deixe esta janela aberta                        |' -ForegroundColor Cyan
Write-Host '  +--------------------------------------------------+' -ForegroundColor Cyan
$nEmpresas = 0
try {
    $nEmpresas = @(Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { -not $_.Name.StartsWith('_') }).Count
}
catch { }
Write-Host ('    Pasta      {0}' -f $root) -ForegroundColor White
Write-Host ('    Porta      {0}   (envio na mesma porta)' -f $port) -ForegroundColor White
Write-Host ('    Passivo    {0}-{1}' -f $pasvFrom, $pasvTo) -ForegroundColor Gray
Write-Host ('    IP publico {0}' -f $publicIp) -ForegroundColor Gray
Write-Host ('    Usuario    {0}  (envio dos clientes)' -f $user) -ForegroundColor Gray
Write-Host ('    Download   e-mail + senha no sqlite.db' ) -ForegroundColor White
Write-Host ('    Banco      {0}' -f $ftpUsersDb) -ForegroundColor Gray
Write-Host ('    Empresas   {0} pasta(s) de CNPJ' -f $nEmpresas) -ForegroundColor White
Write-Host ('    Retencao   {0} completa + {1} do dia, por CNPJ' -f $retencaoCompletos, $retencaoIncrementais) -ForegroundColor Yellow
Write-Host '    Ordem      completo primeiro, depois as do dia' -ForegroundColor Yellow
Write-Host '  +--------------------------------------------------+' -ForegroundColor Cyan
Write-Host ''

$listenEp = New-Object System.Net.IPEndPoint([Net.IPAddress]::Any, $port)
$listener = New-Object System.Net.Sockets.TcpListener $listenEp
try {
    $listener.Start()
}
catch {
    throw "Nao foi possivel abrir a porta $port. Outro FTP (Indy) pode estar usando. Feche o Indy e rode de novo. $($_.Exception.Message)"
}

Write-SrvLog "Escutando 0.0.0.0:$port  raiz=$root"
Write-Host 'Aguardando clientes... (Ctrl+C para parar)' -ForegroundColor Cyan

$iss = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault()
Get-ChildItem Function:\ | ForEach-Object {
    [void]$iss.Commands.Add((New-Object System.Management.Automation.Runspaces.SessionStateFunctionEntry($_.Name, $_.Definition)))
}
foreach ($n in @('root', 'port', 'user', 'pass', 'publicIp', 'pasvFrom', 'pasvTo', 'SrvDir', 'retencaoCompletos', 'retencaoIncrementais', 'ftpUsersExe', 'ftpUsersDb')) {
    $v = Get-Variable -Name $n -ErrorAction SilentlyContinue
    if ($v) {
        [void]$iss.Variables.Add((New-Object System.Management.Automation.Runspaces.SessionStateVariableEntry($v.Name, $v.Value, $null)))
    }
}
$pool = [RunspaceFactory]::CreateRunspacePool(1, 8, $iss, $Host)
$pool.Open()
$running = New-Object System.Collections.Generic.List[object]

function Clear-FtpJobs {
    for ($i = $running.Count - 1; $i -ge 0; $i--) {
        $item = $running[$i]
        if (-not $item.Handle.IsCompleted) { continue }
        try { $item.PS.EndInvoke($item.Handle) } catch { Write-SrvLog ("Cliente: {0}" -f $_.Exception.Message) }
        try { $item.PS.Dispose() } catch { }
        $running.RemoveAt($i)
    }
}

while ($true) {
    try {
        Clear-FtpJobs
        $cli = $listener.AcceptTcpClient()
        $cli.NoDelay = $true
        Write-SrvLog ("Conexao de {0}" -f $cli.Client.RemoteEndPoint)
        $ps = [PowerShell]::Create()
        $ps.RunspacePool = $pool
        [void]$ps.AddScript('Handle-Client -Client $args[0]').AddArgument($cli)
        $running.Add(@{
            PS     = $ps
            Handle = $ps.BeginInvoke()
        })
    }
    catch {
        Write-SrvLog ("Erro: {0}" -f $_.Exception.Message)
    }
}
