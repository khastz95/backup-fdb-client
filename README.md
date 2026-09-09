# Backup Firebird 3.0 + FTP

[![CI](https://github.com/khastz95/backup-fdb-client/actions/workflows/ci.yml/badge.svg)](https://github.com/khastz95/backup-fdb-client/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/khastz95/backup-fdb-client)](https://github.com/khastz95/backup-fdb-client/releases/latest)
[![Licenca](https://img.shields.io/github/license/khastz95/backup-fdb-client)](LICENSE)
[![Plataforma](https://img.shields.io/badge/Windows-10%2F11-0078D4)](https://github.com/khastz95/backup-fdb-client/releases)

Copia de seguranca do banco **Firebird 3.0** no PC do cliente (`nbackup`: completo + incremental), arquivo local e envio a um FTP central. O representante acompanha os CNPJs pelo painel, baixa a cadeia e pode montar um `.fdb`.

**Download:** [Releases](https://github.com/khastz95/backup-fdb-client/releases/latest) · **Senhas:** nunca entram neste Git ([SECURITY.md](SECURITY.md))

## Como encaixa

```mermaid
flowchart LR
  C[Cliente Windows] -->|XSTOR :9099| S[Servidor FTP]
  P[Painel do representante] -->|e-mail + senha| S
  S --> D[(Pasta por CNPJ)]
```

| Quem | Pasta | Programa |
|---|---|---|
| Empresa (cliente) | [`app/`](app/) | `BackupFdbCliente.exe` — [guia](docs/cliente.md) |
| Servidor | [`servidor/`](servidor/) | `iniciar-ftp.bat` na porta **9099** — [guia](docs/servidor.md) |
| Representante | [`painel-monitor/`](painel-monitor/) | `PainelMonitor.exe` — [guia](docs/painel.md) |

A porta **9000** pode continuar com um FTP antigo. Este servico usa **9099** e passivo **50200–50300**.

## Inicio rapido

Requisitos: Windows 10/11, [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48), Firebird 3.0 onde for gerar ou montar `.fdb`.

```bat
git clone https://github.com/khastz95/backup-fdb-client.git
cd backup-fdb-client
build.bat
```

Depois copie os exemplos e preencha **so na sua maquina**:

```bat
copy config.ini.example config.ini
copy servidor\servidor-ftp.ini.example servidor\servidor-ftp.ini
copy painel-monitor\ftp-nativo.ini.example painel-monitor\ftp-nativo.ini
```

| Atalho | Faz |
|---|---|
| `painel.bat` | Abre a interface do **cliente** |
| `executar.bat` | Roda um backup agora |
| `abrir-painel.bat` | Abre o painel do **representante** |
| `servidor\iniciar-ftp.bat` | Sobe o FTP (deixe a janela aberta) |

Guias: [cliente](docs/cliente.md) · [servidor](docs/servidor.md) · [painel](docs/painel.md) · [restaurar](docs/restaurar.md)

## Repositorio

```
app/                 codigo do cliente WinForms
servidor/            FTP PowerShell + usuarios SQLite
painel-monitor/      painel do representante
docs/                guias
.github/             CI, issues e pull requests
config.ini.example   modelo do cliente (sem senha)
```

## Contribuir

Veja [CONTRIBUTING.md](CONTRIBUTING.md) e o [codigo de conduta](CODE_OF_CONDUCT.md). Historico de versoes: [CHANGELOG.md](CHANGELOG.md).

## Licenca

[MIT](LICENSE) © 2026 Alfa Automacao
