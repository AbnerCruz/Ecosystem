# Arquitetura do Urbe

## Princípios

1. **Local-first e offline-first.** O vault pertence ao usuário; nenhuma função essencial depende de backend.
2. **DocumentStore é a autoridade do conteúdo.** Markdown, paths e identidade documental nunca são derivados do aquário.
3. **Identidade é estável.** Rename/move alteram `path`, não o `document.id`.
4. **Aquário é projeção.** Geometria e visualização espacial podem mudar sem reescrever conteúdo.
5. **Mobile-first.** Toque, safe areas, viewport estreito e custo de CPU são contratos de produto.
6. **Uma ação, um comando.** Interfaces legadas devem delegar ao Core em vez de criar uma segunda implementação.
7. **Persistência recuperável.** Gravações usam journal; lixeira e histórico são persistidos no vault.

## Dependências canônicas

```
Vault / Storage Adapter
        ↓
WorkspacePersistence
        ↓
DocumentStore ─────→ KnowledgeIndex
    │     │                 │
    │     ├→ RevisionHistory│
    │     └→ Trash          │
    │                       │
    ├────────→ Explorer     │
    ├────────→ Editor       │
    ├────────→ AI           │
    └→ WorldProjection → AquariumWorld → RoadGraph
                              ↓
                       render / interaction
```

O aquário nunca é necessário para abrir, editar, mover, pesquisar, exportar ou recuperar documentos.

## Responsabilidades

- `core/`: eventos, comandos, documentos, conhecimento, histórico, lixeira e diagnóstico.
- `persistence/`: leitura/gravação do vault, journal e adapters físicos.
- `editor/`: sessão, contexto, navegação, busca/replace e superfícies de edição.
- `explorer/`: árvore canônica, seleção e operações sobre documentos.
- `world/`: projeção espacial, grafo viário, render e interação do aquário.
- `ui/`: superfícies e navegação; não possui dados.
- `src/app.js`: adapter de compatibilidade enquanto UI/render histórico é extraído. Não pode ser fonte de verdade.

## Invariantes de dados

- Conteúdo salvo vem de `DocumentStore`.
- `.urbe/mapa.json` guarda metadados espaciais e IDs, nunca substitui Markdown.
- `.urbe/trash.json` guarda itens recuperáveis.
- `.urbe/history.json` guarda histórico limitado.
- `.urbe/journal.json` existe apenas durante uma transação incompleta; se encontrado na abertura, é usado para recuperação.
- Links semânticos vêm de `KnowledgeIndex`; estradas apenas os representam.
- Rename/move preservam `document.id`.
- Exclusão de nota passa pela lixeira.

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

## Direção de migração

Não adicionar novos wrappers de versão ao fim do monólito. Cada migração substitui uma autoridade antiga por um serviço canônico, adiciona teste do fluxo de produção e só então remove o código substituído.
