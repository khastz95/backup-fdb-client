# Restaurar / montar `.fdb`

A cadeia e **por banco**: um completo novo invalida os incrementais da cadeia anterior. Monte sempre o completo com os incrementais **da mesma pasta** (`Completo_yyyy-MM-dd_HH-mm-ss`).

## Pelo painel

**Baixar cliente** → marque o completo e os incrementais → **Baixar e montar**, ou **Montar .fdb** se os arquivos ja estiverem neste PC.

O arquivo de destino **nao pode existir** (padrao `{CNPJ}_restaurado.fdb`).

## Na linha de comando

As credenciais vao **antes** de `-R`. O `.fdb` de destino nao pode ser o banco em uso.

```bat
nbackup -USER SYSDBA -FETCH_PASSWORD senha.txt -R C:\restaurado\banco.fdb Completo_....nbk Incremental_....nbk
```

Ordem: completo primeiro, depois os incrementais do mais antigo para o mais novo.
