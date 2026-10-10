# ADD-0020 — Ordem do proprietário: o Urbe é a 1.8.4-beta, em C# nativo, sem tecnologia web

**Data:** 2026-10-10. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0043. **Escopo:** produto Urbe e o cliente C# (`apps/urbe/csharp`). **Arquitetura derivada:** [ADR-0032](../../adr/0032-urbe-ui-nativa-sem-webview.md).

## O que o proprietário disse (fatos, com as palavras dele)

1. O problema: *"o Urbe do ecosystem, meu deus, é outro aplicativo, é outra coisa, o pior é que algumas coisas são realmente melhores, mas o conceito e o aplicativo em si não é o urbe"*. *"Isso saiu do meu controle."*
2. A tecnologia: *"o Urbe usava uma tecnologia que eu não quero, é desenvolvido para a web, eu quero um aplicativo desktop e mobile feito em C#, tenho o Lunet por exemplo"*.
3. A referência: *"A versão antiga do urbe é o ponto de partida que eu gostaria para fazer as atualizações"*; *"A versão mais próxima que tenho do urbe antigo é a versão 1.8.4-beta"*. Confirmado: *"Sim, o Urbe é a versão 1.8.4-Beta. É o aplicativo que estou usando enquanto aguardo o Urbe do ecosystem ficar utilizável."*
4. A pilha: perguntado entre Avalonia (um código para Android e Windows) e o caminho do Lunet (Android nativo primeiro), respondeu: *"Contanto que funcione em ambos eu não to nem aí."* A escolha técnica foi **delegada**, com a condição de funcionar em Android **e** desktop.
5. A direção do produto: *"O Urbe antigo é a experiência visual correta"*; do novo, destacou o **editor visual** e que *"o Urbe novo entendeu a nova direção do Urbe, que não é ser um editor de texto como o antigo, mas sim um criador e produtor de artefatos que funciona em cima de todo o sistema de edição de texto"*.

## Decisão consolidada

1. **O Urbe é a 1.8.4-beta.** Ela é a especificação de experiência e visual do cliente C#: a cidade/mundo é o aplicativo (tela cheia, HUD, minimapa, mapa, barra de ferramentas, painel da casa, explorer e editor sobrepostos). Qualquer tela do cliente C# que não exista na 1.8.4 é desvio até ser aprovada como melhoria.
2. **Sem tecnologia web na interface.** O cliente C# desenha sua interface nativamente em Android e Windows, sem WebView, HTML ou CSS como camada de UI. Isso substitui DEC-0035-A (Blazor WebAssembly + MAUI Blazor Hybrid). A pilha concreta fica a critério técnico (ADR-0032), desde que funcione em Android e desktop/Windows.
3. **Direção do produto, preservada e explícita:** experiência da 1.8.4 **+** o Urbe como **criador e produtor de artefatos** (páginas, livros, composições, documentos) sobre o sistema de edição de texto, com o **editor Visual** como peça central dessa produção. O domínio já portado em `Urbe.Core` (vault, Markdown, editor visual, páginas/livros, matemática, mundo) é aproveitado; a casca Blazor não.
4. **Ordem de entrega:** primeiro o Urbe que o proprietário usa hoje (1.8.4) funcionando em C# nativo; depois as melhorias de produção de artefatos, cada uma como item próprio.

## O que não muda

- A 1.8.4-beta publicada continua intacta e é o app de uso diário até o proprietário aprovar o substituto no aparelho.
- DEC-0042/ADD-0019 (sem migração histórica obrigatória; vault limpo com cópia de `.md`) continua valendo.
- Android primeiro; Windows em seguida; sem Web/PWA (direção de 09/10/2026).
- PRs críticos continuam dependendo da autorização de integração do proprietário (ADD-0012/ADR-0015). Esta ordem de direção não é autorização de merge.
- Nenhum PR de outro agente é fechado por este registro; os PRs de UI Blazor em andamento devem ser reconciliados pelos seus autores (ver ADR-0032 §Consequências).

Referências: DEC-0035, DEC-0042, DEC-0043; ADR-0016, ADR-0025, ADR-0032; `apps/urbe/docs/csharp/ROADMAP.md`; `apps/urbe/docs/csharp/PRODUCT-DIRECTION.md`. MANIFEST NN-005, NN-009, NN-011, NN-017, NN-018, NN-020.
