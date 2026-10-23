# Especificação do Atlas Forge

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
| AF-REQ-13 | Primeira abertura vazia; nenhum lugar, nota ou interior inserido sem ação explícita | E2E de autoria e geração local separada |
| AF-REQ-14 | Zoom ancorado até 128×, navegação por dois dedos e escala com resolução nativa | Testes de câmera, antimeridiano, pinch e distinção de células no globo HiDPI |
| AF-REQ-15 | Criar terreno até 1.440 × 720 e refinar mundos existentes preservando autoria e retorno | Refinamento em Worker; teste de footprints e restauração integral do marco |
| AF-REQ-16 | Aproximar do planeta até chão com amostras locais ≤1 m, sem alocar o planeta inteiro | Campo contínuo, tiles em Worker, cache LRU e teste offline planeta/região/terreno/grama |
| AF-REQ-17 | Terreno e altitude não mudam ao trocar LOD ou cruzar tiles/costura | Campo em coordenadas esféricas, residual ancorado e testes de continuidade/reabertura |
| AF-REQ-19 | Aproximar e afastar percorrem a mesma superfície e paleta; pais permanecem durante carregamento; retorno reproduz a imagem | AF-018, pirâmide única do planeta ao chão, wheel e evidência em vídeo |
| AF-REQ-18 | Insolação diária/sazonal em W/m² e balanço global S/4 do modelo circular | AF-016, campo por latitude, editor/inspetor/viewer e testes |
| AF-REQ-12 | Limites científicos e tecnológicos são claros | Classificação A/B na UI e em MODELS.md; sem DEVICE alegado |

O runtime usa HTML/CSS/JS modular e Canvas, sem dependências de produção. Dependências de desenvolvimento: Prettier (formatação estática de fontes; MIT), esbuild (bundle offline em vez de CDN; MIT) e Playwright (Chromium verificável; Apache-2.0). Não entram no runtime Android, tamanho de download do editor ou operação offline. O lockfile é a autoridade de versões resolvidas. A versão do aplicativo é `package.json`; formatos e algoritmo têm identificadores próprios.

Estado atual: AF-016/017 acrescentam insolação sazonal e terreno procedural local; ver [TERRAIN.md](docs/TERRAIN.md).

Não são requisitos concluídos deste corte: solver tectônico, clima validado, lagos dinâmicos, erosão, espécies/populações, geologia temporal, mapas arbitrariamente profundos, polígonos/multisseleção, tiles físicos/refinamento de solvers, edição métrica persistente, recálculo regional, PNG/SVG/GeoJSON, sincronização de arquivo/vault, ZIP com hashes globais, capability pública ou dois Hosts reais.

## Correção de qualidade AF-005/015 — Issue #660

Solicitação do proprietário em 2026-10-22: zoom insuficiente, detalhes ilegíveis e elementos preenchidos sem pedido. Prioridade deste incremento: controle do autor e inspeção cartográfica. Primeira abertura oferece criar/abrir; restauração local mantém o conteúdo existente, inclusive exemplos que já tenham sido salvos, sem apagá-los silenciosamente. **Inspecionar** nunca cria conteúdo. Gerar terreno, lugar e esquema de interior são ações separadas.

A resolução é parâmetro explícito: 360, 720 (padrão da UI) ou 1.440 amostras por longitude, metade em latitude. Zoom de 0,5–128×; grade geográfica adaptativa, escala local no centro e resolução física N–S visíveis. Coastlines e curvas de nível de 250/500 m interpolam somente os dados canônicos. Não há microterreno inventado no renderer. O globo amostra o viewport em até 2× DPR, sem textura global limitada a 640 pixels. A projeção plana repete longitude no antimeridiano.

Refinar é uma regeneração explícita em 1.440 × 720 com a mesma seed e algoritmo. Cada nova célula dentro da pegada de uma célula autoral conserva sua altitude absoluta; clima/drenagem são recalculados e podem mudar com a resolução. Um marco preserva estado, definição e diário anteriores. Se limites de marcos/tamanho forem excedidos, o resultado é recusado antes de substituir o projeto aberto. Sem migração automática ao abrir.

## Retomada AF-016/017 — Issue #664

O proprietário reafirmou física/geologia/meteorologia/química/ecologia e explicitou que quer aproximar até o tile de grama. Respondeu: natureza por seed, lugares opcionais; física natural, magia opcional configurável. O [prompt de execução atualizado](../../docs/proposals/atlas-forge/PROMPT_EXECUCAO.md) registra essas escolhas e remete ao mandato completo.

O terreno local é um campo procedural contínuo em metros, derivado da seed, da grade regional e do algoritmo versionado. O renderer gera blocos sob demanda em Worker e conserva até 96 no LRU; o nível mais fino tem espaçamento N–S ≤1 m. O zoom passa a depender de escala métrica, com presets Planeta/Região/Terreno/Chão; não termina em 128×. A grade física global continua explícita para clima/drenagem. Dados locais não são apresentados como tectônica ou ecologia simuladas. Pincéis atuais são regionais e ficam bloqueados no detalhe para evitar alterações enormes acidentais; edição métrica persistente é o próximo item de dados, ainda pendente.

## Correção AF-018 — Issue #687

O proprietário rejeitou a troca de visualização entre zoom distante e próximo. Requisito: uma única superfície visual em todas as escalas naturais, com continuidade de altura, cobertura, posição e carregamento. Removido o limiar global/local do renderer; pais/filhos usam o mesmo campo e paleta. Atalhos são coordenadas de câmera, nunca modos. Teste específico percorre ida/volta por eventos wheel, sem depender dos atalhos. Esta correção não declara completos edição métrica, tectônica, ecologia ou eras.
