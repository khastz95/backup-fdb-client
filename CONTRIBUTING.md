# Como contribuir

Obrigado por melhorar o backup Firebird. Este repositorio e um conjunto de programas **Windows** (WinForms + PowerShell), compilados com o `csc` do .NET Framework 4.8.

## Fluxo

1. Abra um issue (falha ou ideia) ou descreva a mudanca no PR.
2. Crie um branch a partir de `main`.
3. Rode `build.bat` no Windows.
4. Abra o pull request com o modelo do repositorio.

Nao envie `config.ini`, `servidor-ftp.ini`, `sqlite.db`, logs nem arquivos `.nbk` / `.fdb`.

## Ambiente

- Windows 10 ou 11
- .NET Framework 4.8
- Firebird 3.0 se for testar `nbackup`
- Git e (opcional) GitHub CLI

```bat
git clone https://github.com/khastz95/backup-fdb-client.git
cd backup-fdb-client
build.bat
```

## Estilo

- C# 5 (o compilador do Framework 4.8). Sem interpolacao `$"..."` e sem `nameof`.
- Interface no visual atual (Segoe UI, tons navy/azul).
- Textos da UI em portugues, sem acento em alguns rotulos antigos — mantenha o padrao do arquivo que estiver editando.
- Responda em portugues nos issues e PRs, se possivel.

## Duvidas

Leia [docs/](docs/) e o [README](README.md).
