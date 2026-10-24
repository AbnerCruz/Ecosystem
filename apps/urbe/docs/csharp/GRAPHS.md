# Urbe — Grafos como artefatos visuais

**Escopo:** Urbe C# nativo, Android primeiro. **Direção do proprietário:** 24/10/2026, após detalhar grafos livres, grafos de escopo/hierarquia e grafos legíveis para agentes de IA. Ver ADR-0040. Esta especificação não reintroduz cidade ou simulação.

## Visão do produto

Grafos não são a visualização automática de links do Obsidian. São um **editor visual livre** sobre um canvas espacial, com arquivos e pastas do vault, blocos de texto, desenhos, formas, grupos, relações e artefatos criados pelo usuário. O usuário escolhe o significado do diagrama. Árvores genealógicas são apenas um caso de uso; não se convertem em categoria restritiva.

O editor de grafos é um destino próprio do Urbe. Cada grafo criado é um arquivo comum no vault, reutilizável via `![[Nome]]` ou `![[Pasta/Nome.grafo.json]]` no editor visual Markdown. A incorporação deve continuar navegável, não ser rasterização estática. No Android, gestos do canvas não devem gerar saltos na rolagem do texto.

## Modelo conceitual

1. **Nó livre:** texto, desenho, forma, grupo e outros objetos visuais, sem exigir um arquivo para cada elemento.
2. **Referência:** aponta para arquivo/pasta/nota do vault; nunca guarda os bytes de destino nem adquire posse do arquivo. Remover cartão **não** exclui arquivo.
3. **Relação desenhada:** uma linha ou seta, com rótulo opcional; não implica semântica factual.
4. **Relação tipada:** opcional, explícita, para genealogias, dependências e agentes. IA só a interpreta com indicação de origem e estado de verificação.
5. **Visão estrutural:** derivada das fontes canônicas do vault: pertencimento de pastas, IDs das notas em `.urbe/notas.json`, links `[[nota]]`, backlinks e caminhos. A posição visual escolhida no grafo não renomeia nem move arquivos.
6. **Visão operacional:** etapas, dependências, estado, evidências e aprovações de agentes. Não expõe cadeia de pensamento interna, mas decisões e ações observáveis.

**Fronteiras de autoridade (NN-001):** texto das notas = arquivos Markdown; identidade da nota = NoteLibrary; conexão automática = conteúdo e pasta atuais; canvas autoral = `*.grafo.json`; memória do agente = AssistantStore. Não tornar graph JSON fonte concorrente dos outros sistemas.

## Formato inicial 1

Arquivo UTF-8 `*.grafo.json` dentro do vault, com `type: "urbe.graph"`, `version: 1`, `id`, `title`, `nodes[]` e `edges[]`. Cada nó possui `id`, `kind`, `label`, `target`, `referenceId`, `x`, `y`, `width`, `height`. Cada aresta possui `id`, `sourceId`, `targetId`, `label`, `relation`, `directed`.

- `target` é caminho relativo, normalizado e sem escapar do vault. `referenceId` de nota preserva resolução após rename/move quando a NoteLibrary ainda tiver o ID. Arquivos e pastas seguem por caminho até terem identidade estável própria.
- IDs de nós e arestas não mudam quando o usuário reposiciona, edita ou renomeia um cartão.
- Ler um arquivo malformado ou com `version` futura **deve falhar sem sobrescrever**.
- Gravar por `IUrbeVaultStorage.SaveExistingTextFileAsync`: verificação no disco, detecção de conflito e política atual de recuperação. Não gravar `.urbe/mapa.json` nem `.urbe/mundo.json`.
- Não transformar `*.grafo.json` em nota Markdown na NoteLibrary. É um artefato textual independente, listado no Explorer e incluído em backups/exportações existentes.

## Implementação incremental e verificação

**Fatia 1 — nesta branch:** formato/domínio C#, criação de grafo dentro do vault, destino de navegação, canvas Avalonia básico (pan/zoom/arrastar), inclusão de texto/notas/arquivos/pastas, conexão simples/direcionada, edição e remoção de cartões, escrita verificada e transclusão navegável no editor visual. Testes unitários de serialização, referência, versão futura e integridade. **Sem evidência ainda de build local, CI ou DEVICE.**

**Fatia 2 — a realizar:** ferramentas de desenho e formas geométricas completas, grupos, resize, seleção múltipla, arrastar do Explorer/Área de trabalho, undo/redo, pesquisa incremental para vaults grandes, layout automático, acessibilidade e ações contextuais; testes headless de UI e validação física de toque, teclado e zoom.

**Fatia 3 — a realizar:** grafos automáticos de hierarquia/links/escopo; distinção visual de relações; visualização local/filtros/diferentes layouts; referências quebradas explícitas. Respeitar UC-71/72 como visões distintas, sem substituí-las silenciosamente.

**Fatia 4 — a realizar:** ações de IA sobre grafos com grants explícitos, diffs e aprovações, planejamento observável, proveniência, memórias derivadas do AssistantStore e consistência com UC-87. Não gravar grafo em nome do usuário sem permissão nem criar outro runtime.

**Gates:** testes Core e Client, `dotnet run tests/consistency/Check.cs`, CI e inspeção no Android (NN-017). Não anunciar release ou funcionalidade completa antes destes gates.

## Reuse assessment

A implementação começa em `Urbe.Core` / `Urbe.Client`, com nenhum pacote em `platform/`. Não existe segundo consumidor demonstrado. Extração ao Ecosystem é apenas hipótese futura (NN-022).
