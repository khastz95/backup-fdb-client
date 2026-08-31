# Backup Firebird 3.0 + FTP

Programa para o PC do cliente gerar copia de seguranca do banco **Firebird 3.0** com `nbackup` (completo e incremental), guardar no computador e enviar ao servidor FTP central.

O representante acompanha as empresas pelo **painel**, baixa a cadeia e, se quiser, monta um `.fdb` com o `nbackup`.

> Arquivos com senha **nao entram** neste repositorio. Use sempre os `*.example` e copie para o nome real na sua maquina.

## O que tem no projeto

| Pasta / arquivo | Para quem | O que faz |
|---|---|---|
| `app/` | Cliente | Programa WinForms `BackupFdbCliente.exe` |
| `servidor/` | Servidor | FTP na porta **9099** (a 9000 pode ficar com o servico antigo) |
| `painel-monitor/` | Representante | Lista CNPJs, baixa e monta `.fdb` |
| `config.ini.example` | Cliente | Modelo da configuracao |
| `servidor/servidor-ftp.ini.example` | Servidor | Modelo do FTP e do superuser |
| `painel-monitor/ftp-nativo.ini.example` | Painel | Host e porta do FTP (sem senha) |

## Requisitos

- Windows 10 ou 11
- .NET Framework 4.8
- Firebird 3.0 no PC que gera ou monta o `.fdb` (`nbackup.exe`)
- No servidor: PowerShell 5.1 e (para usuarios de download) as DLLs em `servidor/lib/`

## Como configurar (na sua maquina, nunca no GitHub)

### 1. Cliente

```bat
copy config.ini.example config.ini
```

Edite `config.ini`: CNPJ, caminho do `.fdb`, pasta do Firebird, host FTP, usuario `backupclientes` e **a senha real**.

Compile e abra:

```bat
app\build-cliente.bat
painel.bat
```

Salve, teste o envio e instale o servico (agendador + bandeja).

### 2. Servidor FTP

```bat
copy servidor\servidor-ftp.ini.example servidor\servidor-ftp.ini
```

Edite pasta `E:\Backup2` (ou a sua), IP publico, senha do usuario de envio e a secao `[Superuser]`.

```bat
servidor\usuarios\build-usuarios.bat
servidor\configurar-servidor.bat
servidor\iniciar-ftp.bat
```

Deixe a janela do FTP aberta. No roteador, encaminhe **9099** e a faixa passiva **50200–50300**. A porta **9000** pode continuar no FTP antigo.

Usuarios que baixam pelo painel: `servidor\gerenciar-usuarios.bat` (e-mail + senha no `sqlite.db`).

### 3. Painel do representante

```bat
copy painel-monitor\ftp-nativo.ini.example painel-monitor\ftp-nativo.ini
```

Coloque o host e a porta 9099. Compile:

```bat
painel-monitor\build-painel.bat
abrir-painel.bat
```

O representante entra so com e-mail e senha cadastrados no servidor.

## Seguranca (obrigatorio antes de deixar o GitHub publico)

1. **Nao** envie `config.ini`, `servidor-ftp.ini`, `sqlite.db` nem `config-painel.ini`.
2. Troque senhas que ja tenham sido usadas neste computador (FTP, superuser, SYSDBA se foi de teste).
3. O GitHub **nao apaga historico**: se uma senha ja entrou em um commit, considere essa senha vazada e troque.
4. Exe para o publico: use **Releases** do GitHub, nao grave senha dentro do codigo.

Detalhes em [SECURITY.md](SECURITY.md).

## Restaurar / montar `.fdb`

No painel: **Baixar cliente** → **Baixar e montar**, ou **Montar .fdb** se a cadeia ja estiver neste PC.

Na mao:

```bat
nbackup -USER SYSDBA -FETCH_PASSWORD senha.txt -R C:\restaurado\banco.fdb Completo_....nbk Incremental_....nbk
```

O arquivo de destino **nao pode existir**. As credenciais vao **antes** de `-R`.

## Licenca

Veja [LICENSE](LICENSE).
