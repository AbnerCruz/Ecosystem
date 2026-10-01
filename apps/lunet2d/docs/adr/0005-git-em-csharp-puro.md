# ADR 0005 — Git em C# puro (`Lunet.Git`)

## Contexto

O spec (§22) pede Git progressivo: status, diff, commit, histórico, ramos, revert, push e pull, com GitHub opcional. A biblioteca usual (`LibGit2Sharp`) depende de binários nativos (`libgit2`) que **não existem para Android** — o pacote `LibGit2Sharp.NativeBinaries` traz Windows, Linux e macOS. Executar o `git` de linha de comando também não é possível no aparelho.

## Decisão

Implementar o Git em C# puro, no projeto `Lunet.Git` (sem dependências de outros projetos do Lunet e sem pacotes):

- Formato padrão do Git: objetos soltos e pacotes (`.idx` v2, deltas por deslocamento e por id), índice v2, referências, `packed-refs`, `.gitignore`, `.git/config`. Um repositório criado pelo Lunet abre em qualquer cliente Git, e vice-versa.
- Protocolo "smart" sobre HTTPS (`git-upload-pack` e `git-receive-pack`), com token de acesso pessoal. O transporte é uma interface (`IGitTransport`), o que permite testar o cliente contra o `git` de verdade.
- Descompactador DEFLATE próprio (`Inflate`) porque pacotes recebidos não trazem o tamanho compactado dos objetos.
- Mesclagem em nível de arquivo (sem mesclar linhas): quando os dois lados mudam o mesmo arquivo, o Lunet recusa e explica; a resolução fica para outro cliente.

## Como é validado

Os testes usam o `git` do sistema como oráculo: repositórios criados pelo Lunet passam em `git fsck --strict` e `git status`; repositórios do `git` (inclusive `gc --aggressive`, com deltas) são lidos pelo Lunet; push, clone, fetch e pull rodam contra `git upload-pack`/`git receive-pack` reais pelo mesmo protocolo do HTTP; e os diffs são comparados com `git diff`.

## Consequências

- Funciona offline e no Android sem binários nativos.
- Sem SSH, sem submódulos, sem LFS, sem rebase, sem mesclagem de linhas, sem shallow clone. Índice v3/v4 não é lido.
- Objetos recebidos viram arquivos soltos (não há reindexação de pacotes): adequado a projetos de jogo pequenos.
- O token fica em arquivo no armazenamento privado do app (não em cofre de chaves); isso deve ser revisto antes de distribuir a versão estável.
