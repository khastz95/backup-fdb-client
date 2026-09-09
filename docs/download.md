# Baixar o projeto (um comando)

No PowerShell **como esta**, na pasta onde a pasta `backup-fdb-client` deve nascer:

```powershell
irm https://raw.githubusercontent.com/khastz95/backup-fdb-client/main/install.ps1 | iex
```

Se a politica de execucao bloquear:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/khastz95/backup-fdb-client/main/install.ps1 | iex"
```

## O que o comando faz

1. Clona o Git (ou baixa o ZIP da `main` se nao houver Git)
2. Baixa os `.exe` e exemplos do [Release mais recente](https://github.com/khastz95/backup-fdb-client/releases/latest)
3. Cria `config.ini`, `servidor\servidor-ftp.ini` e `painel-monitor\ftp-nativo.ini` **so se ainda nao existirem**, a partir dos modelos (voce ainda precisa preencher)

Nao envia senha para a internet alem do download publico do GitHub.

## Depois

Siga [configuracao.md](configuracao.md): so neste PC **ou** PC + FTP.
