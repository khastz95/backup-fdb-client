# Cliente (PC da empresa)

O `BackupFdbCliente.exe` gera a cadeia `nbackup` (completo de N em N dias, incremental nos outros) e guarda neste computador. O envio FTP e opcional.

**Passo a passo (local ou FTP):** [configuracao.md](configuracao.md)

`painel.bat` abre a **interface do cliente** (nome antigo). `executar.bat` dispara um backup na hora (`--backup`).

A copia local **nao** e apagada depois de um envio. Retencao no servidor e feita la.

`[Seguranca] SenhaOperacao` no `config.ini` protege sair da bandeja (com servico), remover o servico e limpar copias.
