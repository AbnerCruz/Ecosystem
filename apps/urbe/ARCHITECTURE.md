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
