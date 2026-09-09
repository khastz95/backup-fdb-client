# Cliente (PC da empresa)

O `BackupFdbCliente.exe` gera a cadeia `nbackup` (completo de N em N dias, incremental nos outros), guarda a pasta local e pode enviar ao FTP na porta **9099**.

## Configurar

```bat
copy config.ini.example config.ini
```

Preencha CNPJ, caminho do `.fdb`, pasta do Firebird 3.0, host FTP, usuario `backupclientes` e a senha de envio.

```bat
app\build-cliente.bat
painel.bat
```

`painel.bat` abre a **interface do cliente** (nome historico). `executar.bat` dispara um backup na hora (`--backup`).

Salve, teste o envio e instale o agendamento pela propria tela.

## Destino

- Somente neste PC, ou
- Neste PC e no servidor FTP (`EnviarFtp=1`)

A copia local **nao** e apagada depois do upload. A retencao no servidor e feita la.

## Senha de operacao

`[Seguranca] SenhaOperacao` no `config.ini` protege sair da bandeja (com servico instalado), remover o servico e limpar copias.
