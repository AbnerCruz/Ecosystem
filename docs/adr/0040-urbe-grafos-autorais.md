# ADR-0040 — Grafos autorais como artefatos do Urbe

## Status

Proposto. Integração governada pendente. Direção explicitamente autorizada pelo proprietário em 24/10/2026, depois de descrever um editor livre de grafos no vault, `![[grafo]]` navegável, hierarquia/escopo e usos de IA, e responder "Pode fazer". O desenho detalhado ainda requer CI e validação no Android.

## Contexto

UC-71 (Conexões) e UC-72 (Constelação), segundo DEC-0052/ADD-0029, já representam links de Markdown de forma automática e calma. Não permitem autoria de um grafo arbitrário. A identidade central do Urbe é produzir artefatos concretos locais, sem a simulação visual congelada por DEC-0051. O usuário quer uma nova superfície de edição com nós e arestas livres, arquivos/pastas como referências e composição bidirecional com Markdown.

## Problema

As projeções existentes permitem navegar pelos vínculos automáticos, mas não permitem que o usuário componha grafos persistentes, referências de arquivos e pastas, relações livres e diagramas incorporáveis ao Markdown. É necessário adicionar autoria espacial sem criar concorrência com a organização física do vault nem transformar deduções da IA em fatos.

## Opções

1. Reescrever UC-72 como editor de forças automático.
2. Usar a hierarquia física de pastas como estado editável exclusivo.
3. Criar um formato de grafo autoral independente, com canvas nativo e referências ao vault; conservar UC-71/72 como projeções automáticas.
4. Adotar WebView e um editor web de terceiros.

## Decisão

Opção 3, dentro do cliente C# Avalonia; nenhum WebView novo nem alteração da simulação. Arquivo textual versionado `*.grafo.json` no vault, dono exclusivamente da composição manual: IDs, tipos, posições, rótulos e arestas. Arquivos Markdown e pastas continuam autoridades dos seus conteúdos e organização. `![[grafo]]` embute uma visualização navegável, com editor próprio como destino.

As referências são por caminho relativo, com ID adicional da NoteLibrary para notas. A ausência do destino é mostrada como referência quebrada, não motivo para excluir cartão. Uma mudança de posição no canvas jamais move um arquivo físico. Relação visual não vira fato aceito pela IA automaticamente. Agentes deverão atuar por ferramentas autorizadas, com proveniência e aprovação, após contratos e testes próprios.

A persistência inicial reutiliza os writers verificados de arquivos texto existentes, sem criar um banco compartilhado, sem novo servidor, sem migrar o formato Markdown e sem escrever nos mapas congelados. Leitores recusam versão futura ou arquivo corrompido; a origem permanece intacta.

## Consequências

- Novo formato de dados do usuário exige validação rigorosa de versão, export/import, backup e edição concorrente; até os gates serem satisfeitos a implementação permanece em PR.
- Escopo pessoal/visual e hierarquia real são diferentes; projeção futura do vault será reconstruível, não segunda autoridade.
- GraphRAG, memória contextual e orquestração de tarefas são evoluções distintas, não promessas de funções prontas.
- Gestos, zoom e performance devem ser testados em Android físico, inclusive quando o canvas está incorporado ao editor. Teste em desktop/headless não cumpre NN-017.
- O uso de `*.grafo.json` evita ampliar agora as extensões permitidas nos dois adapters nativos. Reavaliar formato/interop com JSON Canvas com migração explícita.
- Nenhuma extração para `platform/` sem segundo consumidor (NN-022). Nenhuma integração manual à main ou release sem gates.

## Alternativas rejeitadas

- **Opção 1:** confundiria o grafo derivado com a autoria do usuário e colocaria a identidade calma UC-72 em risco.
- **Opção 2:** trataria posições e linhas desenhadas como operações de filesystem, com risco de mover/alterar arquivos involuntariamente.
- **Opção 4:** contrariaria a UI C# nativa sem WebView e criaria dependência de outro runtime e gestos móveis difíceis de controlar.

## Referências

MANIFEST NN-001/005/009/011/016/017/018/019/020/022; AGENTS.md §1.2; ADR-0032/0034/0039; ADD-0028/0029/0030; `apps/urbe/docs/csharp/PRODUCT-DIRECTION.md`; `apps/urbe/docs/csharp/GRAPHS.md`; `apps/urbe/docs/csharp/ROADMAP.md` UC-88.
