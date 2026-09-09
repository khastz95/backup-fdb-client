# Painel do representante

Nao roda no servidor. Lista CNPJs, baixa arquivos e pode montar um `.fdb` com `nbackup`.

## Configurar

```bat
copy painel-monitor\ftp-nativo.ini.example painel-monitor\ftp-nativo.ini
```

Informe host e porta **9099** (sem senha neste arquivo).

```bat
painel-monitor\build-painel.bat
abrir-painel.bat
```

O login e so **e-mail + senha** cadastrados no `sqlite.db`. A pasta local dos downloads pode ser escolhida depois, no botao **Pasta...**.

## Download

- **Baixar arquivo** — um `.nbk` da lista
- **Baixar cliente** — abre a arvore do completo selecionado (e seus incrementais)
- **Baixar e montar** — baixa e junta em um `.fdb` novo (o destino nao pode existir)
- **Montar .fdb** — usa a cadeia ja baixada neste PC

Precisa do Firebird 3.0 neste computador para montar. Usuario/senha padrao da tela de montagem: SYSDBA / senha do banco original.
