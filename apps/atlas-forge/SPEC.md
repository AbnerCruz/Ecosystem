# Especificação do corte vertical AF-001

Fonte: [mandato do proprietário](../../docs/proposals/atlas-forge/MISSION.md), primeira entrega obrigatória A–G. Autorização inicial: construir aplicação independente. DEC-0054/ADR-0035 aprova a incorporação em apps/atlas-forge; integração segue os mecanismos vigentes. Não alterar outro Product ou publicar release por conta própria.

| ID | Comportamento observável | Implementação / evidência |
|---|---|---|
| AF-REQ-01 | Seed e parâmetros geram esfera reproduzível | PRNG por stream; testes de determinismo |
| AF-REQ-02 | Latitude, longitude, áreas e altitudes têm unidades | Datum esférico, raio/altitude em metros, áreas em m² |
| AF-REQ-03 | Costura e polos mantêm conectividade | D8 com reflexão polar; testes de reciprocidade |
| AF-REQ-04 | Editar relevo não se perde na regeneração | Overrides absolutos e diário; teste de igualdade exata |
| AF-REQ-05 | Tarefas longas não bloqueiam a UI e são canceláveis | Worker descartável, job ID e commit só no sucesso |
| AF-REQ-06 | Relevo condiciona clima e drenagem inspecionáveis | Cenários de orografia, bacias fechadas e fluxo conservado |
| AF-REQ-07 | Importar, salvar e reabrir conserva dados | Validador, JSON v2, IndexedDB verificado, download |
| AF-REQ-08 | Contrato Alpha v1 migra para metros sem sobrescrever original | Fixture sintética; todos os pixels importados ficam autorais |
| AF-REQ-09 | Lugar tem nota escolhida e mapa filho persistente | UUID, snapshot Markdown/SHA-256, grafo de salas |
| AF-REQ-10 | Publicação é HTML independente e seguro | Sem dependência do editor/solvers; CSP e teste offline hostil |
| AF-REQ-11 | Voltar e ramificar preserva passado e futuro | Snapshot completo, parent e histórico de comandos |
| AF-REQ-12 | Limites científicos e tecnológicos são claros | Classificação A/B na UI e em MODELS.md; sem DEVICE alegado |

O runtime usa HTML/CSS/JS modular e Canvas, sem dependências de produção. Dependências de desenvolvimento: Prettier (formatação estática de fontes; MIT), esbuild (bundle offline em vez de CDN; MIT) e Playwright (Chromium verificável; Apache-2.0). Não entram no runtime Android, tamanho de download do editor ou operação offline. O lockfile é a autoridade de versões resolvidas. A versão do aplicativo é `package.json`; formatos e algoritmo têm identificadores próprios.

Não são requisitos concluídos deste corte: solver tectônico, clima validado, lagos dinâmicos, erosão, espécies/populações, geologia temporal, mapas arbitrariamente profundos, polígonos/multisseleção, LOD/tile cache, recálculo regional, gestos pinch de dois dedos, PNG/SVG/GeoJSON, sincronização de arquivo/vault, ZIP com hashes globais, capability pública ou dois Hosts reais.
