# Seguranca

Este projeto lida com copias de banco de dados. Trate senhas e o `sqlite.db` como segredo.

## Nunca publique

- `config.ini`
- `servidor/servidor-ftp.ini`
- `servidor/sqlite.db`
- `painel-monitor/config-painel.ini`
- `painel-monitor/ftp-nativo.ini` (pode ter o host interno; o exemplo e publico)
- logs e arquivos `.nbk` / `.fdb`

Use os arquivos `*.example`.

## O que o publico clona

Codigo-fonte, scripts de build, DLLs do SQLite em `servidor/lib/` e modelos de configuracao **sem senha real**.

## Senhas deste computador

Se este repositorio ficar publico e alguma senha ja tiver estado no codigo ou em um `ini` versionado:

1. Troque a senha do FTP de envio no servidor e nos clientes.
2. Troque a senha do superuser (`gerenciar-usuarios.bat`).
3. Revise SYSDBA se o `config.ini` de teste foi compartilhado.

O GitHub guarda o historico. Apagar o arquivo depois **nao** remove a senha dos commits antigos.

## Como enviar um exe para download

1. Compile na sua maquina com os `ini` locais (que nao vao no git).
2. No GitHub: **Releases** → novo release → anexe `BackupFdbCliente.exe` e `PainelMonitor.exe`.
3. Nao coloque senha de FTP no painel. O painel valida e-mail + senha do `sqlite.db`.
