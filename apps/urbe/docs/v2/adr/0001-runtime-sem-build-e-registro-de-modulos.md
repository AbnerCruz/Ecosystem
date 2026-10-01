# ADR-0001 — Runtime sem build com registro explícito de módulos

- Status: Accepted
- Data: 2026-09-30
- Requisitos: REQ-015, REQ-025, REQ-033, REQ-034
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

`index.html` carrega 60 scripts síncronos e a ordem é contrato implícito (AUD-003). Erros de ordem viram funcionalidade ausente silenciosa (guards `if(!core)return`). `sw.js`, `tools/build-www.mjs`, `tools/build-tutorial.mjs` e testes repetem listas/ordens próprias. Não há etapa de build; Web, Electron e Capacitor consomem os mesmos arquivos estáticos.

## Forças

- simplicidade e velocidade de entrega da 1.x;
- verificabilidade estática de dependências;
- paridade Web/Electron/Android sem build;
- ideia futura (declarada pelo proprietário) de um launcher que centralize aplicativos — sem decisão agora.

## Alternativas consideradas

### A — Sem build + registro declarativo de módulos com validação automática

Mantém o modelo atual; custo baixo; a verificação vira testes (`tools/check-modules.mjs`).

### B — ES modules nativos sem bundler

Imports explícitos, mas exige reescrever IIFEs/globals e ajustar SW, Electron e Capacitor.

### C — Bundler (esbuild/Vite)

Melhor carga e tree-shaking; introduz build obrigatório e novo fluxo de contribuição.

### D — Adiar

Mantém a ordem implícita sem verificação.

## Decisão

Adotar **A**. O proprietário decidiu manter o runtime sem build na 2.0-beta; o launcher é ideia futura e fica fora do escopo (REQ-033). Existirá um manifesto declarativo de módulos (`src/modules.json` **[formato exato na SPEC §3]**) com `name`, `file`, `provides`, `requires`, `phase`; `index.html`, `sw.js` e `tools/build-www.mjs` são gerados ou validados a partir dele por `tools/check-modules.mjs`, que rejeita dependência sem provedor anterior, ciclo e arquivo fora do manifesto. ES modules/bundler ficam ADIADOS (REQ-034).

## Consequências positivas

- ordem e dependências deixam de ser convenção
- nenhuma mudança de modelo de execução, sem impacto em plataformas

## Consequências negativas / trade-offs

- manter um manifesto além de `index.html`
- não elimina globals `window.Urbe*` (apenas os declara)

## Migração

Gerar primeiro o manifesto a partir do `index.html` atual (sem mudar a ordem), depois passar `index.html`/`sw.js` a derivados; cada extração do `app.js` registra seu módulo.

## Validação

`tools/check-modules.mjs` no CI (G0); `tests/consistency.mjs` passa a ler o manifesto; teste de boundary por camada (REQ-015).

## Relações

- substitui: —; documentos afetados: SPEC §3, ROADMAP RM-F2-*, ARCHITECTURE-MAP
