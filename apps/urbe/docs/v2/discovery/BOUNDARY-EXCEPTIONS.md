# Exceções conhecidas de boundary (REQ-015)

> Lista **fechada e decrescente** das dependências que violam as camadas de SPEC §3.1 (`app → ui → features → persistence → core/kit/native → vendor`; `world` e `pages` não dependem de `ai`, exceto adaptadores `*/ai-tools.js`). `tools/check-modules.mjs` falha se surgir violação fora desta tabela **ou** se uma linha daqui deixar de ser violação (remova-a no mesmo PR).
> A coluna "Remoção" indica o item do ROADMAP que elimina a exceção. A camada de cada módulo vive em `src/modules.json`.

| Módulo | Depende de | Motivo [F] | Remoção |
|---|---|---|---|
| `src/native/bridge.js` | `src/persistence/workspace.js` | reage a `workspace:loaded` para soltar o espelho em memória (`bridge.js:132-135`) | RM-F1-12 |
| `src/ui/touch-debug.js` | `src/app.js` | usa `legacy.runtime` (versão) | RM-F2-15 |
| `src/explorer/mobile-ui.js` | `src/app.js` | consome `legacy.runtime` e `workspace.storage` (importar/exportar ZIP) | RM-F2-15, RM-F2-16 |
| `src/ai/tools.js` | `src/ui/quick-open.js` | ferramenta de busca usa o serviço `quickOpen` | RM-F5-04 |
| `src/pages/engine.js` | `src/app.js` | usa serviço provido por `app.js` (renderização de Markdown) | RM-F2-09 |
| `src/customize/customize.js` | `src/app.js` | `world.custom` (`app.js:5192`) | RM-F2-16 |
| `src/customize/plugins.js` | `src/app.js` | `legacy.runtime`, `world.custom` | RM-F2-15, RM-F2-16 |
| `src/tutorial/tutorial.js` | `src/app.js` | `legacy.runtime.ensureFolders` | RM-F2-15 |
| `src/world/life.js` | `src/app.js` | `world.life.host` (`app.js:5535`) | RM-F2-16 |
| `src/pages/studio.js` | `src/ai/ui.js` | reutiliza o painel do agente (`UrbeAgentUI`) | RM-F2-01 (decisão de hotspot) |
| `src/ui/settings.js` | `src/app.js` | `legacy.runtime`, `workspace.storage` | RM-F2-15, RM-F2-16 |
