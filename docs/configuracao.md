# Configuracao passo a passo

Os arquivos `*.example` deste repositorio sao **modelos vazios**. Qualquer `config.ini` ou `servidor-ftp.ini` numa maquina de desenvolvimento e **so teste local** e **nao** vai para o GitHub.

Ha dois modos no cliente (etapa **5. Para onde vai a copia**):

1. [Somente neste computador](#a-somente-neste-computador) — sem FTP
2. [Neste computador e no servidor FTP](#b-neste-computador-e-no-servidor-ftp)

Requisitos comuns: Windows 10/11, [Firebird 3.0](https://firebirdsql.org/) no PC que gera (ou monta) o `.fdb`, [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) para compilar.

Baixar o projeto de uma vez: [download.md](download.md).

---

## A. Somente neste computador

Use quando ainda nao ha servidor, ou o cliente nao deve enviar nada pela rede.

### 1. Ter o programa

- Pelo [Release](https://github.com/khastz95/backup-fdb-client/releases/latest) (`BackupFdbCliente.exe`), ou
- Pelo comando em [download.md](download.md), ou
- `build.bat` depois de clonar o Git

### 2. Criar o `config.ini`

Na pasta do `BackupFdbCliente.exe`:

```bat
copy config.ini.example config.ini
```

O exemplo vem com `EnviarFtp=0`. Se voce abrir o programa e marcar **Somente neste computador**, isso tambem grava `EnviarFtp=0` ao **Salvar**.

### 3. Preencher na tela (nao use dados de outro PC)

Abra `painel.bat` (e a tela do **cliente**, nao o painel do representante).

| Etapa na tela | O que informar |
|---|---|
| CNPJ / nome | Da **esta** empresa |
| Pasta do Firebird | Em geral `C:\Program Files\Firebird\Firebird_3_0` |
| Arquivo `.fdb` | Caminho real do banco (ex.: `C:\GDI\Dados\GDI.FDB`) |
| Usuario / senha do banco | SYSDBA e a senha **deste** Firebird |
| Pasta das copias | Pasta **local**, de preferencia fora do OneDrive |
| Completo a cada N dias | Ex.: 4 |
| Guardar N completos | Ex.: 3 (a mais antiga sai so neste PC) |
| Destino | **Somente neste computador (sem servidor FTP)** |
| Hora | Ex.: 23:00 |

Nao precisa clicar em **Servidor...** nem preencher host FTP.

### 4. Testar e instalar

1. **Salvar**
2. **So neste PC** (ou **Copiar agora**) — deve nascer pasta `Completo_...` com o `.nbk`
3. **Instalar servico agora** — agenda o backup diario e a bandeja

Pronto. O incremental dos outros dias so faz sentido junto com o completo da **mesma** pasta.

---

## B. Neste computador e no servidor FTP

Ordem: **servidor primeiro**, depois cada cliente, depois (opcional) o painel do representante.

### B1. Servidor (uma vez)

No PC que vai guardar as copias de todas as empresas:

1. Baixe o projeto ([download.md](download.md) ou Git).
2. Copie o modelo:

   ```bat
   copy servidor\servidor-ftp.ini.example servidor\servidor-ftp.ini
   ```

3. Edite `servidor\servidor-ftp.ini` **com os dados deste servidor** (nao copie IP, senha ou e-mail de um ambiente de teste):

   | Chave | Significado |
   |---|---|
   | `Pasta` | Ex.: `E:\Backup2` (a unidade precisa existir) |
   | `Porta` | `9099` (deixe `9000` para um FTP antigo, se houver) |
   | `PassivaInicio` / `PassivaFim` | `50200` e `50300` |
   | `IpPublico` | IP ou host que os clientes alcancam (DDNS) |
   | `[Usuario] Login` / `Senha` | Conta de **envio** dos programas dos clientes |
   | `[Superuser] Email` / `Senha` | Primeiro admin do `sqlite.db` (sem a palavra `TROQUE`) |

4. Compile usuarios, crie pasta/firewall e ligue o FTP:

   ```bat
   servidor\usuarios\build-usuarios.bat
   servidor\configurar-servidor.bat
   servidor\iniciar-ftp.bat
   ```

5. Deixe a janela do FTP **aberta**. No roteador, encaminhe **9099** e **50200–50300** para este PC.

6. Representantes: `servidor\gerenciar-usuarios.bat` e cadastre e-mails de download (nao e a conta `backupclientes`).

Detalhes extras: [servidor.md](servidor.md).

### B2. Cliente (cada empresa)

1. `copy config.ini.example config.ini` ao lado do `BackupFdbCliente.exe`.
2. Abra `painel.bat`. Preencha Firebird e pasta local como no modo A.
3. Marque **Neste computador e no servidor FTP**.
4. **Servidor...** — host (o mesmo DDNS/IP do FTP), porta **9099**, usuario e senha de **envio** (os de `[Usuario]` no servidor, nao o e-mail do painel).
5. **Salvar**.
6. **Testar envio** — tem que aparecer `XSTOR=1` no log; senha errada ou FTP antigo na 9000 falha aqui.
7. **Copiar agora** (completo + envio). Confira a pasta `E:\Backup2\{CNPJ}_{Nome}\` no servidor.
8. **Instalar servico agora**.

`PastaRemota` padrao `E:/Backup2` e o caminho **no servidor**. O cliente nao precisa ter o disco `E:`.

### B3. Painel do representante (notebook)

So depois do FTP da versao 2 estar no ar (`iniciar-ftp.bat`).

1. `copy painel-monitor\ftp-nativo.ini.example painel-monitor\ftp-nativo.ini`
2. Host e porta **9099** (sem senha neste arquivo).
3. `abrir-painel.bat` — entre com **e-mail e senha** cadastrados no servidor.
4. **Pasta...** se quiser mudar onde os downloads caem neste PC.

Guia de botoes: [painel.md](painel.md). Restaurar `.fdb`: [restaurar.md](restaurar.md).

---

## Se algo falhar

| Sintoma | O que checar |
|---|---|
| `nbackup.exe nao encontrado` | Firebird 3.0 instalado; pasta correta na tela |
| Destino recusou `...:9099` | `iniciar-ftp.bat` aberto; firewall; encaminhamento 9099 |
| Login recusado no painel | E-mail do `sqlite.db`, nao o usuario FTP de envio |
| Servidor sem `XRETR=1` | Ainda e o FTP antigo; use o `iniciar-ftp.bat` deste projeto |
