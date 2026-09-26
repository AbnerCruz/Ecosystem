# Arquitetura do Urbe

## Princípios

1. **Local-first e offline-first.** O vault pertence ao usuário; a aplicação não depende de backend.
2. **Notas são a fonte da verdade.** A simulação pode ler atividade e topologia, mas não altera conteúdo para satisfazer mecânicas do jogo.
3. **Estado derivado não contamina o vault.** Burgo, cache, rotas e estado transitório ficam fora dos arquivos do usuário.
4. **Mobile-first.** Toda mudança deve preservar toque, safe areas e execução em navegador móvel.
5. **Compatibilidade antes de refatoração.** A v0.26 começa separando assets sem alterar contratos do runtime.

## Camadas atuais

- `index.html`: shell e superfícies DOM.
- `src/styles/base.css`: estilos históricos/base.
- `src/styles/shell.css`: casca visual mais recente.
- `src/legacy/bootstrap.js`: dependências/runtime legado que precede o app.
- `src/app.js`: runtime principal atual. Ainda é monolítico e será decomposto por domínio.
- `sw.js`: cache offline do app shell.
- IndexedDB: estado interno, snapshots e Burgo.
- Vault: arquivos do usuário e `mapa.json`.

## Fronteiras-alvo

A decomposição do `src/app.js` deve ocorrer nesta ordem, mantendo contratos explícitos:

1. `core/`: eventos, estado compartilhado, utilidades e ciclo de vida.
2. `persistence/`: IndexedDB, File System Access, vault e sincronização.
3. `world/`: terreno, câmera, regiões, construções, estradas e pathfinding.
4. `editor/`: edição, preview, wiki-links e formatos.
5. `explorer/`: árvore, seleção, mover/importar/excluir.
6. `burgo/`: simulação, economia, moradores, crônica e pedestres.
7. `ai/`: provedor, contexto, artefatos e histórico.
8. `ui/`: navegação, sheets, dock, menus e feedback.

Nenhum módulo de simulação poderá escrever em notas. Nenhum módulo de UI deverá persistir dados diretamente.

## Contratos de regressão

Antes de cada extração:
- o PWA deve instalar e iniciar offline;
- vault existente deve abrir sem migração destrutiva;
- editor deve abrir/salvar e preservar wiki-links;
- mundo deve reconstruir estradas e regiões;
- Burgo deve carregar o mesmo estado por vault;
- `window.URBE` e `window.Burgo` permanecem disponíveis para diagnóstico;
- assets extraídos devem constar no cache do service worker.

## Estratégia

A v0.26 é a fundação: separa documento, estilos e runtime, adiciona verificação automática e documenta fronteiras. As próximas extrações devem mover um domínio por vez e substituir wrappers históricos por um único ponto de implementação, nunca criar novos patches de versão no fim do arquivo.


## Workspace Core — v0.27 foundation

Urbe is treated as a local-first knowledge application with an integrated spatial aquarium, not as a game that happens to contain an editor.

The canonical dependency direction is:

```
Documents / Vault
       ↓
  Workspace Core
  ├─ Events
  ├─ Commands
  ├─ State
  └─ Services
       ↓
Editor · Explorer · Search · AI · Aquarium
                                  ↓
                                Burgo
```

Rules introduced in v0.27:

- Productive features must be callable through stable commands instead of UI-specific handlers.
- New subsystems communicate through Core events/services; they must not wrap unrelated global functions.
- The aquarium is a consumer/interface of workspace state, never the source of truth for document content.
- Burgo remains downstream of the aquarium/workspace and cannot be required for editing or opening a vault.
- Legacy code is migrated incrementally through `legacy.runtime`; compatibility bridges are temporary and replaceable.
- A command has one canonical ID. Keyboard shortcuts, buttons, menus, gestures and the future command palette must invoke the same command.


## v0.33 — Aquarium runtime boundary

The canonical dependency direction is now enforced in executable services:

```
Vault adapter
    ↓
Persistence Core
    ↓
DocumentStore ─→ KnowledgeIndex
    ↓               ↓
WorldProjection → AquariumWorld → RoadGraph
    ↓
render / interaction adapters
    ↓
Burgo (optional downstream simulation)
```

Invariants:

- Markdown content is never sourced from aquarium entities.
- Aquarium entities carry document identity plus spatial presentation only.
- Semantic roads come from the KnowledgeIndex; tile paths are a rendering/materialization concern.
- Burgo may observe documents and aquarium state but may not mutate document content.
- Simulation/tick work should register with the shared Scheduler instead of creating independent perpetual timers.
- Mobile interaction remains the primary interaction contract; desktop shortcuts and layouts are additive.
