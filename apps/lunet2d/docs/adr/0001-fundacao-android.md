# ADR 0001 — Fundação Android nativa em C#

## Contexto

Lunet precisa funcionar diretamente no Android, offline, sem depender de uma IDE de desktop. O risco maior é executar C# escrito no aparelho e depois empacotar um jogo independente.

## Decisão

Começar com .NET 10 for Android e controles Android nativos para a interface inicial. Persistir cada projeto como arquivos comuns no armazenamento privado do aplicativo. Manter o manifesto de projeto e o código de jogo independentes das classes Android. Antes de desenvolver as ferramentas avançadas, validar Roslyn, Preview, OpenGL ES e empacotamento no aparelho.

## Consequências

A interface inicial é pequena e o projeto ainda não executa jogos. Projetos salvos em `FilesDir` são privados ao aplicativo; exportação/importação via Storage Access Framework serão necessários para controle pleno pelo usuário. O build em CI não comprova o comportamento em aparelho.

## Alternativas

- MAUI: acrescenta uma camada multiplataforma sem benefício para um aplicativo Android only.
- WebView como editor inteiro: facilitaria interface, mas não resolveria compilação/execução e criaria uma dependência inadequada para a base.
