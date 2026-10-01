# Performance — baseline e plano (Urbe 1.8.2-beta)

> Descoberta da issue #33 (REQ-009, 070–075). **[F]** fato, **[I]** inferência, **[P]** proposta.
> Não há medição reproduzível no repositório. Este documento inventaliza o que existe, fixa cenários e métricas e **adia os budgets numéricos** para depois do baseline (REQ-071).

## 1. Medições citadas (não reproduzíveis)
[F] CHANGELOG/PRs, ambiente do PR, sem script no repo:
- v1.7.2: abertura no Android de ~560 chamadas de ponte para 5 (espelho em memória).
- v1.7.1: retângulos desenhados por quadro com cidade parada ~1570→~50; arrastar em celular simulado (CPU 6×) 12→~30 fps; diário a cada 5 s ~1 MB→~7 KB.
- v1.8.0: ~34 fps parado com tudo ligado em celular médio simulado; moradores 20→~30 fps; fauna 12→25 fps.
- [F] Nenhum budget numérico em arquivo algum; nenhum `performance.now`, benchmark ou budget em `tests/`, `tools/`, `.github`.

## 2. Inventário por cenário

### Abertura
- [F] 60 scripts síncronos sem `defer/async` (`index.html:208-267`), ~1,8 MB de `src/`; eager: `app.js` 438 KB, `legacy/bootstrap.js` 97 KB (JSZip), `katex.min.js` 272 KB (`index.html:246`), `pages/engine.js` 106 KB, `pages/studio.js` 84 KB, `ai/ui.js` 44 KB, `tutorial/content.js` 113 KB (`index.html:255`).
- [F] Lazy só para PDF.js (`import()` de CDN, `app.js:2894-2897`) e Worker de chunks (`app.js:4806`).
- [F] Script inline aplica aparência antes do primeiro desenho (`index.html:26`).
- [F] `persistence/workspace.js:9-18`: `adapter.read` **sequencial** por arquivo (`for` + `await`), depois `store.replaceAll`.
- [F] Nativo: `tree()`/`readTexts()` em lote com espelho em memória (`bridge.js:43-65`); no Electron o espelho é solto em `workspace:loaded` (`bridge.js:132-135`), chokidar cuida das mudanças; textos >4 MB fora do espelho (`bridge.js:52`, `vault-fs.js:44`, `readTexts` Java); `walk` Java profundidade 32.
- [F] SW pré-cacheia ~60 arquivos no install, rede-primeiro 4 s (`NETWORK_TIMEOUT`, `sw.js:109`), `skipWaiting` (`:121`); desregistrado no app nativo (`bridge.js:144`).

### Edição/salvamento
- [F] Debounce 900 ms (`workspace.js:5`); `pagehide`/`visibilitychange` forçam flush (`:65-67`).
- [F] `flush` chama `desired()` (`:23`), que **recalcula o mapa completo** (todos os docs, `mapa.json`, `trash.json`, `history.json`, `compositions.json`) e compara por string com o snapshot **a cada flush** → custo O(vault) por gravação. → REQ-072.
- [F] Histórico: até 40 revisões de conteúdo completo por documento (`history.js:4`, janela 5 s); `history.json` reserializado por inteiro. [I] cresce com vaults grandes. → REQ-075.
- [F] Diário completo só em operação multi-arquivo (`workspace.js:32`). Electron: fila por arquivo + tmp/rename (`vault-fs.js:49-58`); Android: base64 pela ponte Capacitor (`bridge.js:228-235`).

### Busca
- [F] Quick-open (`ui/quick-open.js:12-32`): a cada consulta varredura linear de **todos** os documentos com `fold` (NFD) de título, path e **conteúdo completo**, sem índice/cache/debounce visível; limite 50 resultados.
- [F] `KnowledgeIndex.search` (`core/knowledge-index.js:25-31`): tokens invertido, mas varre `store.list()` para título/path.
- [I] Ponto crítico previsível em vaults com milhares de notas. → REQ-073.

### Cidade
- [F] Chão via Worker (`world/chunk-worker.js`); `URBE_CHUNKS_MAX = innerWidth<900 ? 56 : 96` (LRU, `app.js:4799`); ≤6 pedidos em voo; fallback síncrono 8 ms/quadro (`:4814,4860`); visão de longe `URBE_LONGE=.5`; caches `terrCache` (600), `v22AguaCache` (150k), `urbeArvoresCache` (400); rotas de moradores com orçamento 150000 passos (`app.js:3550`).
- [F] `Scheduler` central (`core/scheduler.js`): um `requestAnimationFrame`, jobs com `interval`/`whenVisible`, pausa com `document.hidden`; renderer redesenha só se "sujo" ou a cada 500 ms (`world/renderer.js:7`); moradores a 33 ms (`app.js:4058`).
- [F] Qualidade adaptativa (`world/life.js:422-425`): `lagMedio>58` reduz `q` até 0,35; `<40` sobe até 1; sem zoom (`v.z<.12`) a animação para.
- [F] Opções de desligar moradores/fauna/clima/eventos/vidro em Personalização.
- [F] Serviços canônicos do aquário não estão ligados em produção (ARCHITECTURE-MAP §4); [I] a otimização precisa ocorrer sobre o renderer que de fato roda (REQ-026).

### Memória
- [F] Todos os documentos, history (40/doc), lixeira e composições em RAM; espelho nativo duplica textos até a soltura no desktop; caches com teto. [F] Nenhuma métrica de memória; nenhum budget por classe de aparelho.

## 3. Métricas e classes de dispositivo [P]
| Classe | Definição inicial | Referência de teste |
|---|---|---|
| D1 Desktop | Windows/Chromium sem limitação | Electron + navegador |
| D2 Celular médio | CPU throttling 4–6×, 4 GB | emulação Playwright/CDP; Android real quando possível |
| D3 Celular baixo | throttling 6×+, 2 GB | emulação |

| Métrica | Como medir |
|---|---|
| Tempo até shell utilizável (TTI) | `performance.mark` no boot até `core:ready` e primeiro desenho |
| Tempo até vault carregado | `workspace:loaded` − início do load; nº de chamadas de ponte (nativo) |
| Latência de digitação (p95) | tempo entre input e frame no editor com nota de 100 KB |
| Custo de flush | ms e bytes por `flush` após editar 1 char, por tamanho de vault |
| Busca | ms p95 por consulta em 50/500/5000 notas |
| Cidade | fps parado, fps arrastando, chunks/s, memória JS |
| Memória | `performance.memory`/heap snapshot após abrir vault |
| Offline | abrir sem rede após 1ª visita (SW) |

## 4. Cenários do harness (REQ-070) [P]
1. Vault sintético `S` 50 / `M` 500 / `L` 5000 notas (tamanho médio 4 KB, 10% com links, 5% assets) gerado deterministicamente por `tools/perf/make-vault.mjs`.
2. Abrir vault (frio, com SW e sem).
3. Digitar 200 caracteres em nota de 100 KB.
4. Salvar após editar 1 nota em `S/M/L` (custo do flush).
5. Buscar 20 consultas em `S/M/L`.
6. Cidade parada e arrastando por 10 s em D2.
7. Primeira abertura com criação do Tutorial (46 notas).
8. Android com e sem `MANAGE_EXTERNAL_STORAGE` (manual/instrumentado).
Resultados versionados em `docs/v2/perf/baseline-<data>.json` com ambiente e commit.

## 5. Budgets [C: adiados com gate]
- **[C]** Nenhum budget numérico é definido antes do baseline (REQ-070). O gate G5 exige: (a) harness executável e reproduzível; (b) baseline publicado; (c) budgets propostos como **limite de regressão relativa** (ex.: ±10% do baseline por métrica) até serem fixados como absolutos pelo proprietário.
- [P] Orçamentos iniciais absolutos a validar com o baseline: TTI ≤ 2 s em D1 e ≤ 4 s em D2 para `M`; flush de 1 char independente do tamanho do vault (O(1) em bytes gravados); busca p95 ≤ 50 ms em `L` em D1.

## 6. Otimizações planejadas (por REQ)
- REQ-072 persistência incremental (diff por documento; `mapa` e side-files regravados só se sujos).
- REQ-073 índice de busca pré-normalizado (título/path/conteúdo fold no `document:*`), debounce e limite.
- REQ-074 lazy-load de Studio, IA, KaTeX, `tutorial/content.js` sob demanda; load do vault com concorrência limitada.
- REQ-075 compactação/teto de `history.json` e gravação incremental.
- REQ-026 renderer canônico religado antes de qualquer otimização de cidade.
