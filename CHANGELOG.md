# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).
Versionamento aproximado de [SemVer](https://semver.org/lang/pt-BR/).

## [Nao publicado]

### Adicionado

- Passo a passo de configuracao so local ou com FTP (`docs/configuracao.md`)
- Download do projeto com um comando PowerShell (`install.ps1`)

## [1.0.0] - 2026-08-31

### Adicionado

- Cliente WinForms com `nbackup` (completo e incremental), copia local e envio FTP (XSTOR na porta 9099)
- Servidor FTP em PowerShell com retencao por CNPJ, XTREE/XRETR e usuarios de download em SQLite
- Painel do representante (login por e-mail, download e montagem de `.fdb`)
- Configuracoes de exemplo sem senha e release inicial no GitHub

[1.0.0]: https://github.com/khastz95/backup-fdb-client/releases/tag/v1.0.0
