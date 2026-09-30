# Performance — harness e baselines (REQ-009, REQ-070, REQ-071)

## Como medir
```
node tools/perf/run.mjs --vault S|M|L [--runs 3] [--cpu 1|4|6] [--scenarios open,edit,save,search,city,tutorial] [--out arquivo.json]
```
- Vaults sintéticos determinísticos (`tools/perf/make-vault.mjs`): S=50, M=500, L=5000 notas (~4 KB/nota, 10% com `[[links]]`, 5% assets).
- O harness abre o app real em Chromium (Playwright), semeia o vault no IndexedDB (modo interno) e observa: eventos do core, `requestAnimationFrame`, `performance.memory` e contadores de gravação no IndexedDB injetados **pelo harness** (o app não é modificado).
- `--cpu N` aplica *throttling* de CPU (D2 ≈ 4×, D3 ≈ 6×).

## Cenários e métricas
| Chave | Cenário (PERFORMANCE.md §4) |
|---|---|
| `open_ms` | navegar → `core:ready` com todos os documentos carregados (frio, IDB semeado) |
| `heap_after_open_mb` / `heap_after_city_mb` | `usedJSHeapSize` |
| `save_after_edit_ms`, `save_bytes_written`, `save_idb_puts` | custo do flush após editar 1 nota (após aquecimento) |
| `search_ms` | 20 consultas no quick-open (custo síncrono do `input`) |
| `typing_ms_per_char` | 100 inserções em nota de ~100 KB no editor Visual até o próximo quadro |
| `city_idle_fps`, `city_drag_fps` | fps da cidade parada e arrastando |
| `first_open_with_tutorial_ms` | primeira abertura com criação do Tutorial |

## Baselines
Arquivos `baseline-<versão>-<vault>-<classe>.json` com commit, ambiente (Node, Chromium, CPU throttle) e resultados. Os números dependem da máquina: compare **sempre no mesmo ambiente** (o CI usa o mesmo runner). Em ambiente headless sem GPU o fps da cidade é baixo e serve só como referência relativa.

## Budgets
`budgets.json` (REQ-071) é derivado do baseline e verificado por `npm run perf:check` (RM-F5-01/02).
