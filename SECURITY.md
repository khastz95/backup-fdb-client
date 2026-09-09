# Seguranca

Este software trata **copias de banco de dados**. Senhas, `sqlite.db` e arquivos `.nbk` / `.fdb` sao segredo.

## Como reportar

Nao abra issue publico para vazamento de credencial ou falha de autenticacao.

1. Use [Security advisories](https://github.com/khastz95/backup-fdb-client/security/advisories/new), ou
2. Escreva para **alfa@alfaautomacao.com.br**

Descreva o impacto e como reproduzir, **sem** anexar bancos reais.

## Nunca publique no Git

- `config.ini`
- `servidor/servidor-ftp.ini`
- `servidor/sqlite.db`
- `painel-monitor/config-painel.ini`
- `painel-monitor/ftp-nativo.ini`
- logs e backups

Use sempre os `*.example`.

## Releases

Os `.exe` do [Release](https://github.com/khastz95/backup-fdb-client/releases) nao devem carregar senha de FTP. O cliente le `config.ini` local; o painel valida e-mail no `sqlite.db` do servidor.

Se uma senha entrou em um commit, **troque-a**. O historico do GitHub nao some ao apagar o arquivo depois.
