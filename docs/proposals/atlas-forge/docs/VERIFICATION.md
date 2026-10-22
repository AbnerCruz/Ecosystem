# Evidências de execução — AF-001

Evidências locais e remotas em 22/10/2026. Os registros de CI indicam explicitamente o commit testado; nenhuma evidência representa validação humana em aparelho.

| Verificação | Resultado |
|---|---|
| `npm run lint` | Prettier: fontes, scripts, testes e HTML consistentes |
| `npm test` | 17 testes: geometria, seed, áreas oceânicas, clima/orografia, drenagem/conservação/endorreia, edição, marcos, migração, persistência, Markdown, SHA-256, grafo local e boundary do viewer |
| `npm run build` | Editor offline (~84 KiB) e exemplo Miravento com JSON, nota e viewer |
| `npm run test:e2e` | 12 cenários completos em Chromium 141; [relatório](evidence/browser-report.json), zero exceções JS |
| Editor offline | Aberto por `file://` com rede desativada; Worker incorporado operacional |
| Viewer offline | Aberto por `file://` com rede desativada; zero requisições HTTP, nota hostil inerte e mapa filho navegável |
| Dados | Undo/redo exato; regeneração preserva máscaras; roundtrip JSON integral; versão futura rejeitada; cópia IndexedDB verificada e reaberta após reload |
| CI remoto Atlas Forge | [Run 39845060375](https://github.com/AbnerCruz/Ecosystem/actions/runs/39845060375) verde no commit `a6d2145a`; 17 testes + 12 cenários + build e artefatos |
| CI remoto governança | [Run 39844932792](https://github.com/AbnerCruz/Ecosystem/actions/runs/39844932792) verde no commit `005f34cf`: checks, piloto P6-4, self-test e simulação do integrador |
| Ecosystem baseline | `dotnet run tests/consistency/Check.cs`: 22 checks passaram antes e depois das mudanças normativas; estado final no handoff |
| Android físico | **Pendente**. Viewport móvel/toque emulado não é DEVICE, instalação ou lifecycle Android |
| Alpha real | **Indisponível**. Fixture sintética não demonstra paridade com o aplicativo original |

Capturas verificadas visualmente: [desktop](evidence/desktop.png), [globo](evidence/globe.png), [viewport móvel](evidence/mobile.png), [viewer offline](evidence/offline-viewer.png).

## Benchmark reproduzível

`npm run benchmark` e `npm run benchmark:render`. Máquina da sessão: Linux, AMD EPYC 9V74, Node 24.19.0, Chromium 141 headless. Mediana de três gerações aquecidas, após uma execução fria; sem garantias sobre aparelhos do usuário. Números completos: [geração](evidence/benchmark.txt), [renderização](evidence/render-benchmark.txt).

| Grade | Células | Geração, clima e hidrologia (mediana) | Serialização JSON | Projeto sem marcos |
|---|---:|---:|---:|---:|
| 90×45 | 4.050 | 10,4 ms | 1,3 ms | 75,7 KiB |
| 180×90 | 16.200 | 32,6 ms | 2,8 ms | 301,6 KiB |
| 360×180 | 64.800 | 105,0 ms | 8,4 ms | 1.205,2 KiB |

No Canvas de 900×650 CSS px, mediana de desenho síncrono: mapa 0,5 ms, globo 3,9 ms (p95 6,0 ms), com lookup aquecido. O globo original recalculava a projeção inversa em cada pixel a cada rotação: mediana de 83,3 ms na mesma sessão. O cache eliminou esse gargalo sem alterar a grade física. Tempo síncrono de render não equivale ao frame apresentado na tela; primeiro desenho/reconstrução do lookup, thread de composição, consumo de energia, exportação grande e toque físico ainda precisam de medição específica. A memória reportada é heap do processo Node no momento da amostra, não pico de RAM nem memória do navegador.

## Limites de aceite

M0/M1 não estão declarados integralmente concluídos: falta corpus Alpha real, incorporação formal/revisão/integração e Android físico. M2–M8 não estão entregues como um todo. O primeiro sistema causal é demonstrado em cenários sintéticos; não há simulação científica geral. O roadmap registra os itens ainda não implementados.
