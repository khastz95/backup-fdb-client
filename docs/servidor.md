# Servidor FTP

**Passo a passo completo** (so neste PC ou com FTP): [configuracao.md](configuracao.md). Os `*.example` sao modelos; configs de teste na maquina de quem desenvolve **nao** entram no Git.

## Configurar

```bat
copy servidor\servidor-ftp.ini.example servidor\servidor-ftp.ini
```

Ajuste pasta (`E:\Backup2` ou a sua), IP publico, senha do usuario de **envio** e a secao `[Superuser]` (primeiro admin do `sqlite.db`).

```bat
servidor\usuarios\build-usuarios.bat
servidor\configurar-servidor.bat
servidor\iniciar-ftp.bat
```

Deixe a janela do `iniciar-ftp.bat` aberta. No roteador, encaminhe **9099** e a faixa passiva **50200–50300**.

## Dois tipos de login

| Quem | Credencial | Pode |
|---|---|---|
| Programa do cliente | usuario/senha em `[Usuario]` | Enviar (XSTOR), criar pastas |
| Representante | e-mail/senha no `sqlite.db` | Listar e baixar (XTREE/XRETR) |

Cadastro de e-mails: `servidor\gerenciar-usuarios.bat`.

## Retencao

Por pasta de CNPJ: 1 completo + ate 6 incrementais (configuravel). A cadeia mais antiga sai sozinha apos um envio novo.
