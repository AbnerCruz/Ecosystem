# Verificação cartográfica — AF-005/015

Pedido: Issue [#660](https://github.com/AbnerCruz/Ecosystem/issues/660). Fonte canônica: SPEC AF-REQ-13/14/15. Versão 0.3.0-alpha.1; não é uma declaração de conclusão da missão nem homologação Android.

## Comportamentos verificados

- Primeira abertura vazia e sem geração em background; Gerar cria somente terreno. Notas/lugares existentes continuam preservados na recuperação/importação.
- Ação explícita para gerar o esquema de nove salas; inspecionar só abre o conteúdo persistido.
- Alta resolução 1.440×720: 1.036.800 amostras, roundtrip integral JSON e nenhuma entidade acrescentada.
- Câmera plana e globo: mesmo ponto geográfico sob o cursor após zoom 40×; erro < 1e−8 graus no cenário. Limite 128× e pan através do antimeridiano.
- Globo com viewport 1.000×750 em DPR 2: projeção em 2.000×1.500 pixels, células nativas adjacentes distinguíveis em zoom 16×. O teste anterior com textura global 640 não passaria nesse critério.
- Dois dedos em sequência de eventos touch do Chromium aumentam zoom e deslocam centro sem confirmar um pincel. Não substitui aparelho real.
- Refinamento conserva altitudes na pegada dos overrides, entidades e fonte original sem mutação. Roundtrip e retorno ao marco restauram grade, estado e diário completos.
- E2E (14 cenários): cancelamento de importação assíncrona impede commit tardio; pincel/undo/redo, regeneração, cancelamento, marcos/ramificação, nota hostil/hash, JSON, versão futura, persistência IndexedDB, editor/viewer offline e viewport móvel. Cenário adicional percorre refinamento 720→1.440, limite de checkpoint e restauração na interface.

Comandos: `npm run check`, `npm run benchmark`, `npm run benchmark:render`; consistência: `dotnet run tests/consistency/Check.cs`. Logs e capturas são produzidos em `test-results` e no artefato CI; handoff registra o resultado final e o commit/PR.

## Medições locais de desempenho

Node 24.19.0, Linux cloud, AMD EPYC 9V74. Valores não são promessa de tempo em celular. `benchmark` gera quatro vezes e usa mediana das três execuções aquecidas.

| Grade | Células | Geração aquecida | JSON aproximado |
|---|---:|---:|---:|
| 180×90 | 16.200 | 37 ms | 302 KiB |
| 720×360 | 259.200 | 486 ms | 4.819 KiB |
| 1.440×720 | 1.036.800 | 2.086 ms | 19.276 KiB |

Chromium 141, viewport 900×650 CSS px, grade física 720×360, 12 amostras síncronas por configuração. Raster/caches preparados; cold mede primeiro render daquela configuração. Não mede apresentação do compositor nem latência completa do gesto.

| Vista | Cold | Mediana | P95 |
|---|---:|---:|---:|
| Plano 1× | 90 ms | 0,4 ms | 1,2 ms |
| Plano 8× | 52 ms | 2,7 ms | 6,6 ms |
| Plano 64× | 2 ms | 2,1 ms | 3,6 ms |
| Globo 1× | 66 ms | 5,3 ms | 24,3 ms |
| Globo 8× | 99 ms | 19,3 ms | 20,9 ms |
| Globo 64× | 109 ms | 20,1 ms | 22,2 ms |

Não se alega 60 fps constante. O lookup do globo é reconstruído quando a câmera muda; o raster colorido pode ser reutilizado se fonte/câmera forem idênticas. Curvas são cache derivado, grade é recortada ao viewport nas aproximações. Tiles físicos, invalidação regional e cálculo multirresolução continuam AF-005.

## Limites de informação e compatibilidade

A melhor grade representa 27,8 km N–S por célula no raio terrestre. Aproximar 128× permite posicionar lugares e inspecionar amostras, mas não fornece ruas ou terreno de metros. Curvas de nível são interpoladas do campo canônico; não são observações extras. A função de terreno e os modelos heurísticos não foram apresentados como geologia validada.

JSON v2 continua igual; leitor 0.3 admite grades maiores. O leitor anterior recusa grades >360 com erro explícito. O máximo continua 24 MiB: em alta resolução sobra menos espaço para marcos. Operações de Worker e checkpoint são verificadas antes do commit, sem truncar o passado. Projetos antigos não são refinados nem têm exemplos removidos silenciosamente.
