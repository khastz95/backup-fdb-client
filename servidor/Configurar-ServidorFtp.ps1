#Requires -Version 5.1
#Requires -RunAsAdministrator
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'servidor-ftp.ini')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

function Get-IniValue {
    param($Path, $Section, $Key, $Default = '')
    if (-not (Test-Path -LiteralPath $Path)) { return $Default }
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

function Invoke-Native {
    param([string]$File, [string[]]$Arguments)
    $old = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $File @Arguments 2>&1 | Out-Null
        $code = 0
        if (Test-Path variable:LASTEXITCODE) { $code = $LASTEXITCODE }
        return $code
    }
    finally { $ErrorActionPreference = $old }
}

function Save-ServidorIni {
    param($Path, $Cfg)
    $body = @"
; Servidor FTP do backup Firebird
; Rode este arquivo no COMPUTADOR DO SERVIDOR (nao no cliente)

[Servidor]
Pasta=$($Cfg.Pasta)
Porta=$($Cfg.Porta)
PassivaInicio=$($Cfg.PasvIni)
PassivaFim=$($Cfg.PasvFim)
IpPublico=$($Cfg.Ip)

[Retencao]
Completos=$($Cfg.Completos)
Incrementais=$($Cfg.Incrementais)

[Usuario]
Login=$($Cfg.Login)
Senha=$($Cfg.Senha)
"@
    $utf8 = New-Object System.Text.UTF8Encoding $true
    [IO.File]::WriteAllText($Path, $body.Replace("`n", "`r`n"), $utf8)
}

function Apply-ServidorPasta {
    param($Cfg)
    $drive = [IO.Path]::GetPathRoot($Cfg.Pasta).TrimEnd('\')
    if (-not (Test-Path -LiteralPath $drive)) {
        throw "A unidade $drive nao existe neste computador."
    }
    if (-not (Test-Path -LiteralPath $Cfg.Pasta)) {
        New-Item -ItemType Directory -Path $Cfg.Pasta -Force | Out-Null
    }
    foreach ($grant in @('*S-1-5-18:(OI)(CI)F', '*S-1-5-32-544:(OI)(CI)F', '*S-1-5-32-545:(OI)(CI)M')) {
        $null = Invoke-Native icacls.exe @($Cfg.Pasta, '/grant', $grant, '/C')
    }
    foreach ($name in @('Backup Firebird FTP v2 comando', 'Backup Firebird FTP v2 passivo')) {
        $null = Invoke-Native netsh.exe @('advfirewall', 'firewall', 'delete', 'rule', "name=$name")
    }
    $null = Invoke-Native netsh.exe @('advfirewall', 'firewall', 'add', 'rule', 'name=Backup Firebird FTP v2 comando', 'dir=in', 'action=allow', 'protocol=TCP', "localport=$($Cfg.Porta)")
    $null = Invoke-Native netsh.exe @('advfirewall', 'firewall', 'add', 'rule', 'name=Backup Firebird FTP v2 passivo', 'dir=in', 'action=allow', 'protocol=TCP', "localport=$($Cfg.PasvIni)-$($Cfg.PasvFim)")
}

$cfg = @{
    Pasta        = Get-IniValue $ConfigPath 'Servidor' 'Pasta' 'E:\Backup2'
    Porta        = [int](Get-IniValue $ConfigPath 'Servidor' 'Porta' '9099')
    PasvIni      = [int](Get-IniValue $ConfigPath 'Servidor' 'PassivaInicio' '50200')
    PasvFim      = [int](Get-IniValue $ConfigPath 'Servidor' 'PassivaFim' '50300')
    Ip           = Get-IniValue $ConfigPath 'Servidor' 'IpPublico' '127.0.0.1'
    Completos    = [int](Get-IniValue $ConfigPath 'Retencao' 'Completos' '1')
    Incrementais = [int](Get-IniValue $ConfigPath 'Retencao' 'Incrementais' '6')
    Login        = Get-IniValue $ConfigPath 'Usuario' 'Login' 'backupclientes'
    Senha        = Get-IniValue $ConfigPath 'Usuario' 'Senha' ''
}

$navy = [Drawing.Color]::FromArgb(15, 76, 129)
$accent = [Drawing.Color]::FromArgb(37, 99, 235)
$bg = [Drawing.Color]::FromArgb(241, 245, 249)
$mute = [Drawing.Color]::FromArgb(71, 85, 105)
$ui = New-Object Drawing.Font('Segoe UI', 9.5)
$title = New-Object Drawing.Font('Segoe UI', 16, [Drawing.FontStyle]::Bold)

$form = New-Object Windows.Forms.Form
$form.Text = 'Servidor de copias'
$form.Font = $ui
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.ClientSize = New-Object Drawing.Size(560, 640)
$form.BackColor = $bg

$header = New-Object Windows.Forms.Panel
$header.Dock = 'Top'
$header.Height = 72
$header.BackColor = $navy
$l1 = New-Object Windows.Forms.Label
$l1.Text = 'Servidor de copias'
$l1.Font = $title
$l1.ForeColor = [Drawing.Color]::White
$l1.Location = New-Object Drawing.Point(20, 8)
$l1.AutoSize = $true
$l2 = New-Object Windows.Forms.Label
$l2.Text = 'Pasta, porta, envio dos clientes e usuarios de download.'
$l2.ForeColor = [Drawing.Color]::FromArgb(191, 219, 254)
$l2.Location = New-Object Drawing.Point(22, 42)
$l2.AutoSize = $true
$header.Controls.AddRange(@($l1, $l2))
$form.Controls.Add($header)

function New-Lbl([string]$Text, [int]$X, [int]$Y) {
    $c = New-Object Windows.Forms.Label
    $c.Text = $Text
    $c.Location = New-Object Drawing.Point($X, $Y)
    $c.Size = New-Object Drawing.Size(150, 22)
    $c.TextAlign = 'MiddleLeft'
    return $c
}
function New-Txt([int]$X, [int]$Y, [int]$W, [string]$Val) {
    $c = New-Object Windows.Forms.TextBox
    $c.Location = New-Object Drawing.Point($X, $Y)
    $c.Size = New-Object Drawing.Size($W, 26)
    $c.Text = $Val
    return $c
}
function New-Num([int]$X, [int]$Y, [int]$W, [decimal]$Min, [decimal]$Max, [decimal]$Val) {
    $c = New-Object Windows.Forms.NumericUpDown
    $c.Location = New-Object Drawing.Point($X, $Y)
    $c.Size = New-Object Drawing.Size($W, 26)
    $c.Minimum = $Min
    $c.Maximum = $Max
    $c.Value = [Math]::Max($Min, [Math]::Min($Max, $Val))
    return $c
}
function New-Btn([string]$Text, [int]$X, [int]$Y, [int]$W, [bool]$Primary) {
    $c = New-Object Windows.Forms.Button
    $c.Text = $Text
    $c.Location = New-Object Drawing.Point($X, $Y)
    $c.Size = New-Object Drawing.Size($W, 32)
    $c.FlatStyle = 'Flat'
    $c.Cursor = [Windows.Forms.Cursors]::Hand
    if ($Primary) {
        $c.BackColor = $accent
        $c.ForeColor = [Drawing.Color]::White
        $c.FlatAppearance.BorderSize = 0
    }
    else {
        $c.BackColor = [Drawing.Color]::White
        $c.ForeColor = [Drawing.Color]::FromArgb(30, 41, 59)
        $c.FlatAppearance.BorderColor = [Drawing.Color]::FromArgb(203, 213, 225)
    }
    return $c
}

$g1 = New-Object Windows.Forms.GroupBox
$g1.Text = '1. Pasta e rede'
$g1.Location = New-Object Drawing.Point(16, 88)
$g1.Size = New-Object Drawing.Size(528, 168)
$g1.BackColor = [Drawing.Color]::White
$form.Controls.Add($g1)

$g1.Controls.Add((New-Lbl 'Pasta das copias' 14 28))
$txtPasta = New-Txt 160 26 350 $cfg.Pasta
$g1.Controls.Add($txtPasta)

$g1.Controls.Add((New-Lbl 'Porta FTP' 14 62))
$numPorta = New-Num 160 60 80 1 65535 $cfg.Porta
$g1.Controls.Add($numPorta)
$g1.Controls.Add((New-Lbl 'Passivo de' 260 62))
$numPasv1 = New-Num 340 60 70 1 65535 $cfg.PasvIni
$g1.Controls.Add($numPasv1)
$g1.Controls.Add((New-Lbl 'ate' 416 62))
$numPasv2 = New-Num 450 60 70 1 65535 $cfg.PasvFim
$g1.Controls.Add($numPasv2)

$g1.Controls.Add((New-Lbl 'IP publico' 14 96))
$txtIp = New-Txt 160 94 350 $cfg.Ip
$g1.Controls.Add($txtIp)

$hintRede = New-Object Windows.Forms.Label
$hintRede.Text = 'A porta 9000 fica com o FTP antigo. Este servidor usa 9099. Encaminhe 9099 e 50200-50300 no roteador.'
$hintRede.ForeColor = $mute
$hintRede.Location = New-Object Drawing.Point(14, 130)
$hintRede.Size = New-Object Drawing.Size(500, 28)
$g1.Controls.Add($hintRede)

$g2 = New-Object Windows.Forms.GroupBox
$g2.Text = '2. Quanto guardar por empresa (CNPJ)'
$g2.Location = New-Object Drawing.Point(16, 266)
$g2.Size = New-Object Drawing.Size(528, 112)
$g2.BackColor = [Drawing.Color]::White
$form.Controls.Add($g2)

$g2.Controls.Add((New-Lbl 'Copias completas' 14 28))
$numComp = New-Num 160 26 60 1 5 $cfg.Completos
$g2.Controls.Add($numComp)
$g2.Controls.Add((New-Lbl 'Copias do dia' 240 28))
$numInc = New-Num 340 26 60 1 30 $cfg.Incrementais
$g2.Controls.Add($numInc)

$hintRet = New-Object Windows.Forms.Label
$hintRet.Text = 'Recomendado: 1 completa + 6 do dia. Sempre nessa ordem. A mais antiga sai sozinha.'
$hintRet.ForeColor = $mute
$hintRet.Location = New-Object Drawing.Point(14, 64)
$hintRet.Size = New-Object Drawing.Size(500, 36)
$g2.Controls.Add($hintRet)

$g3 = New-Object Windows.Forms.GroupBox
$g3.Text = '3. Usuario de envio (programas dos clientes)'
$g3.Location = New-Object Drawing.Point(16, 388)
$g3.Size = New-Object Drawing.Size(528, 92)
$g3.BackColor = [Drawing.Color]::White
$form.Controls.Add($g3)
$g3.Controls.Add((New-Lbl 'Usuario' 14 28))
$txtUser = New-Txt 160 26 140 $cfg.Login
$g3.Controls.Add($txtUser)
$g3.Controls.Add((New-Lbl 'Senha' 310 28))
$txtPass = New-Txt 360 26 150 $cfg.Senha
$txtPass.UseSystemPasswordChar = $true
$g3.Controls.Add($txtPass)

$g4 = New-Object Windows.Forms.GroupBox
$g4.Text = '4. Usuarios de download (painel)'
$g4.Location = New-Object Drawing.Point(16, 490)
$g4.Size = New-Object Drawing.Size(528, 72)
$g4.BackColor = [Drawing.Color]::White
$form.Controls.Add($g4)
$hintUsers = New-Object Windows.Forms.Label
$hintUsers.Text = 'Quem baixa pelo painel entra com e-mail + senha do sqlite.db.'
$hintUsers.ForeColor = $mute
$hintUsers.Location = New-Object Drawing.Point(14, 28)
$hintUsers.Size = New-Object Drawing.Size(330, 28)
$g4.Controls.Add($hintUsers)
$btnUsers = New-Btn 'Gerenciar e-mails' 360 24 150 $false
$g4.Controls.Add($btnUsers)
$btnUsers.Add_Click({
        $exe = Join-Path $PSScriptRoot 'FtpUsers.exe'
        if (-not (Test-Path -LiteralPath $exe)) {
            [Windows.Forms.MessageBox]::Show(
                "Compile primeiro:`nservidor\usuarios\build-usuarios.bat",
                'Usuarios de download',
                'OK',
                'Warning'
            ) | Out-Null
            return
        }
        Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot
    })

$lblStatus = New-Object Windows.Forms.Label
$lblStatus.Location = New-Object Drawing.Point(16, 578)
$lblStatus.Size = New-Object Drawing.Size(280, 28)
$lblStatus.ForeColor = $mute
$lblStatus.Text = 'Salve e aplique para criar a pasta e liberar o firewall.'
$form.Controls.Add($lblStatus)

$btnSave = New-Btn 'Salvar e aplicar' 304 576 150 $true
$btnCancel = New-Btn 'Fechar' 462 576 82 $false
$form.Controls.AddRange(@($btnSave, $btnCancel))

function Read-FormCfg {
    if ([string]::IsNullOrWhiteSpace($txtPasta.Text)) { throw 'Informe a pasta das copias.' }
    return @{
        Pasta        = $txtPasta.Text.Trim()
        Porta        = [int]$numPorta.Value
        PasvIni      = [int]$numPasv1.Value
        PasvFim      = [int]$numPasv2.Value
        Ip           = $txtIp.Text.Trim()
        Completos    = [int]$numComp.Value
        Incrementais = [int]$numInc.Value
        Login        = $txtUser.Text.Trim()
        Senha        = $txtPass.Text
    }
}

$btnCancel.Add_Click({ $form.Close() })
$btnSave.Add_Click({
        try {
            $novo = Read-FormCfg
            Save-ServidorIni -Path $ConfigPath -Cfg $novo
            Apply-ServidorPasta -Cfg $novo
            $lblStatus.ForeColor = [Drawing.Color]::FromArgb(21, 128, 61)
            $lblStatus.Text = 'Pronto. Pasta e firewall ok. Ligue com iniciar-ftp.bat.'
            [Windows.Forms.MessageBox]::Show(
                "Servidor configurado.`n`nPasta: $($novo.Pasta)`nPor CNPJ: $($novo.Completos) completa(s) + $($novo.Incrementais) do dia.`n`nAgora rode iniciar-ftp.bat e deixe a janela aberta.",
                'Servidor de copias',
                'OK',
                'Information'
            ) | Out-Null
            try { Start-Process explorer.exe $novo.Pasta } catch { }
        }
        catch {
            $lblStatus.ForeColor = [Drawing.Color]::FromArgb(185, 28, 28)
            $lblStatus.Text = $_.Exception.Message
            [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Servidor de copias', 'OK', 'Warning') | Out-Null
        }
    })

[void]$form.ShowDialog()
