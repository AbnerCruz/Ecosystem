# Arquitetura do Urbe

## Princípios

1. **Local-first e offline-first.** O vault pertence ao usuário; nenhuma função essencial depende de backend.
2. **DocumentStore é a autoridade do conteúdo.** Markdown, paths e identidade documental nunca são derivados do aquário.
3. **Identidade é estável.** Rename/move alteram `path`, não o `document.id`.
4. **Aquário é projeção.** Geometria e visualização espacial podem mudar sem reescrever conteúdo.
5. **Mobile-first.** Toque, safe areas, viewport estreito e custo de CPU são contratos de produto.
6. **Uma ação, um comando.** Interfaces legadas devem delegar ao Core em vez de criar uma segunda implementação.
7. **Persistência recuperável.** Gravações usam journal; lixeira e histórico são persistidos no vault.

## Camadas e dependências (SPEC §3.1)

As dependências apontam **para baixo**; uma dependência só pode ir para a mesma camada ou inferior:

```
app          src/app.js                       composição/bootstrap + adapters legacy (temporário)
 └ ui        command palette, quick-open, settings, tips
    └ features  editor · explorer · world · pages · ai · customize · composition · math · tutorial
       └ persistence   WorkspacePersistence (+ adapters do vault)
          └ core · kit · native     Core, DocumentStore, History, Trash, Knowledge, Scheduler ·
                                    ícones/diálogos · contrato UrbeNative (web | electron | android)
             └ vendor               KaTeX, JSZip
```

- `src/modules.json` é a **fonte única dos módulos** (nome, arquivo, camada, fase, `provides`, `requires`, `uses`). `index.html` e `sw.js` são derivados dele (`node tools/check-modules.mjs --write`) e o CI valida ordem, dependências não declaradas e boundaries de camada (`tools/check-modules.mjs`, `tests/boundaries.mjs`).
- As exceções conhecidas às camadas estão em `docs/v2/discovery/BOUNDARY-EXCEPTIONS.md` (lista fechada e decrescente; cada linha aponta o item do ROADMAP que a remove).
- `world` e `pages` não dependem de `ai` (exceto adaptadores `*/ai-tools.js`).
- O runtime **não tem etapa de build** (ADR-0001).

## Fluxo canônico de dados

```
Vault / Storage Adapter → WorkspacePersistence → DocumentStore ─→ KnowledgeIndex
                                                   │  ├→ RevisionHistory
                                                   │  └→ Trash
                                                   ├→ Explorer · Editor · AI
                                                   └→ WorldProjection → AquariumWorld → RoadGraph → render / interação
```

O aquário nunca é necessário para abrir, editar, mover, pesquisar, exportar ou recuperar documentos.

### Estado real em relação ao alvo (2.0)
- **Fato:** `aquarium.renderer`, `aquarium.touch` e `aquarium.roads` existem e têm teste, mas a cidade em produção ainda é desenhada e interagida pelo código histórico de `src/app.js` (laço `frame`, `draw*`, handlers `canvas*`). O caminho canônico acima é o **alvo** (REQ-026), não o estado atual.
- **Fato:** o editor (renderização Markdown, modo Visual, autocompletar de wikilinks) e o acesso físico ao vault (`FS`/`Disco`/`DBK`) ainda moram em `src/app.js` (REQ-027, REQ-028).
- `src/app.js` só pode diminuir: `tools/check-debt.mjs` falha se sobrescritas, duplicatas, camadas "Antes/Base" ou atribuições de `V21_VERSION` aumentarem (REQ-012). A versão do produto vem de uma fonte única (`package.json`, `tools/version.mjs`).
- Cadeia normativa: `docs/v2/REQUIREMENTS.md` → `SPEC.md` → `ROADMAP.md` → `TRACEABILITY.md`.

## Responsabilidades

- `core/`: eventos, comandos, documentos, conhecimento, histórico, lixeira e diagnóstico.
- `persistence/`: leitura/gravação do vault, journal e adapters físicos.
- `editor/`: sessão, contexto, navegação, busca/replace e superfícies de edição.
- `explorer/`: árvore canônica, seleção e operações sobre documentos.
- `world/`: projeção espacial, grafo viário, render e interação do aquário.
- `ui/`: superfícies e navegação; não possui dados.
- `ai/`: provedores de modelo, ferramentas sobre o vault, agentes, loop agêntico e interface do Assistente. Toda escrita passa pelas ferramentas e pode ser desfeita.
- `pages/`: motor de páginas (`.page.json` → HTML de arquivo único), modelos, estúdio e ferramentas do Assistente.
- `math/`: extração e renderização de LaTeX (KaTeX em `vendor/`) e o editor de fórmulas.
- `customize/`: `Personalização/tema.json` (validado, aplicado como variáveis CSS e cache local), temas salvos, estilos CSS, pacotes de texturas, plugins (aprovação por aparelho com SHA-256 do código, API com limpeza automática) e a tela de Personalização.
- `tutorial/`: a pasta Tutorial. O conteúdo é gerado de `tutorial/*.md` por `tools/build-tutorial.mjs`, que confere links e tags; o CI falha se o arquivo gerado estiver desatualizado.
- `src/app.js`: adapter de compatibilidade enquanto UI/render histórico é extraído (ver "Estado real"). Não pode ser fonte de verdade nem receber nova regra de domínio.

## Serviços de extensão

- `world.custom` (em `src/app.js`): opções da cidade, luz do dia, paleta e texturas do chão (repassadas ao worker de chunks), sprites próprios e camadas desenhadas por plugins. É a única porta do resto do app para o render histórico.
- `customize`: lê e grava o `tema.json`, aplica e avisa `customize:applied`.
- `plugins`: lista, liga, desliga e sincroniza plugins; nunca executa código sem aprovação neste aparelho.
- `tutorial`: cria a pasta uma vez por cidade (marca em `.urbe/tutorial.json` e no aparelho) e restaura sob pedido.

## Invariantes de dados

- Conteúdo salvo vem de `DocumentStore`.
- `.urbe/mapa.json` guarda metadados espaciais e IDs, nunca substitui Markdown.
- `.urbe/trash.json` guarda itens recuperáveis.
- `.urbe/history.json` guarda histórico limitado.
- `.urbe/journal.json` existe apenas durante uma transação incompleta; se encontrado na abertura, é usado para recuperação.
- Links semânticos vêm de `KnowledgeIndex`; estradas apenas os representam.
- Rename/move preservam `document.id`.
- Exclusão de nota passa pela lixeira.
- Aparência, estilos, texturas e plugins moram no vault (`Personalização/`); só a aprovação de plugins e o cache de aparência ficam no aparelho.
- Tags e links dentro de código (blocos ``` e trechos `...`) não contam.
- O modo Visual do editor serializa de volta sem perdas: tabelas, callouts, listas aninhadas, linguagem do código e rótulos de link.

## Contratos de regressão

Toda mudança deve manter:
- inicialização e PWA offline;
- abertura de vault antigo sem migração destrutiva;
- criação, edição, rename, move, exclusão/restauração e reload;
- journal de recuperação;
- wiki-links/backlinks;
- abertura e interação do aquário no viewport mobile;
- documentos funcionais com aquário desativado;
- nenhum polling permanente de UI quando eventos resolvem o problema.
- `tests/consistency.mjs`: todo arquivo de `src/` no `index.html` e no service worker, e uma versão só em todo lugar.

## Direção de migração

Não adicionar novos wrappers de versão ao fim do monólito. Cada migração substitui uma autoridade antiga por um serviço canônico, adiciona teste do fluxo de produção e só então remove o código substituído.
