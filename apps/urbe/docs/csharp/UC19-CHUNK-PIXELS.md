# UC-19 / LEGACY-2 — Composição contínua dos chunks 1.8.4-beta

Referência normativa: `UC19-VISUAL-ACCEPTANCE.md`, `UC19-PROCEDURAL-CITY.md`, `ROADMAP.md`, requisito de não reformular a arte. Oráculo técnico: `src/world/pixel-art.js::chunkPixels` (SHA `d47cf2902e8aeb5ca2c529d2e4c9617fb71ca931`) e `src/world/terrain.js` (SHA `7d4a1e88b153a6d942f968572a0260d5154e42ef`).

## Incremento

`Urbe.Core.LegacyChunkPixels.Render` compõe, **em tempo de execução C#**, buffers RGBA de 256×256 pixels para cada chunk 16×16, usando:

- Bioma e elevação derivados de `LegacyTileSampler`/`LegacyTileChunks`, incluindo halo de um tile em cada borda.
- As 19 paletas e 4 variantes do gerador `LegacyWorldPixelTextures`, sem arquivos PNG pré-gerados.
- Sombreamento da elevação com gradiente noroeste e manchas de cor (ruído de valor) usando os mesmos seeds do JS.
- Interpolação de biomas deformada por ruído, ecótonos terra–terra, água × terra, espuma costeira e profundidade da água.
- Decoração original por tile/slot delegada a `LegacyVegetation.DecorAt` e `LegacyWorldGroundDecor.Paint`.

Nenhum formato de arquivo, contrato de vault, saída de release, host, importação ou dado de usuário foi alterado. O buffer é uma etapa interna de renderização, não uma nova fonte de verdade. A Cidade C# agora o consome por viewport sob demanda.

## Testes e limites

`LegacyChunkPixelsTests` confere comprimento/alpha, pixels de textura em água profunda com sombreamento, coordenadas negativas, determinismo, diferenças entre chunks, transição água/terra, decoração por tile e proteção de entradas. O teste `LegacyChunkPixelOracleTests` invoca **somente no CI/teste** o `pixel-art.js::chunkPixels` congelado, executado pelo `chunk-pixels-oracle.mjs` via Node, e compara **todos os bytes RGBA** de quatro cenários canônicos (oceano profundo, costa com decoração, misturas de bioma/relevo e chunks negativos). A equivalência depende de passar no CI; screenshots do mesmo cenário e aceite Android físico continuam pendentes.

**Fatia de integração:** `LegacyChunkViewport` seleciona apenas os chunks que cruzam a câmera, inclusive em coordenadas negativas. `LegacyWorldScene` constrói uma única vez em tarefa assíncrona o mundo original com seed `urbe` e spawn `(36,25)`, usa cache FIFO limitado a 16 PNGs transportados como data URI e projeta seus chunks no componente `World.razor`. Pan em saltos de 8 tiles, scroll nativo e zoom de pixel próximo ao original funcionam sem invocar o gerador JS no runtime. Os handlers para casas, bairros, seleção, criação e salvamento SAF foram preservados.

**Limite explícito:** somente o terreno é contínuo. Os 64 lotes clicáveis continuam sendo o overlay provisório `CityTileLayout` 8×8, agora posicionado no espaço do mundo no ponto de origem `(32,24)`; fora dessa área o terreno pode ser explorado, mas não se constroem novas notas por toque ainda. Árvores com sprites altos, estradas `[[wikilink]]`, cidadãos, regiões e HUD original ainda precisam ser compostos. A interface permanece pendente de aceite visual Android. Se a geração falhar, `World.razor` preserva o tabuleiro anterior como fallback. Nenhum PNG estático é fonte da arte; data URI serve somente como transporte dos pixels C# ao WebView.

## Próxima ação

1. Executar o CI .NET e consistency no estado combinado da PR, corrigindo regressões.
2. Conferir o resultado da comparação byte a byte do oráculo JS `chunk-pixels-oracle.mjs` contra o C# e corrigir qualquer discrepância sem alterar os algoritmos ou as expectativas legadas.
3. Testar o novo viewport/câmera/cache com a compilação Android e Windows e smoke no dispositivo; corrigir diferenças de posicionamento e interação antes de ampliar a área de edição.
4. Integrar estradas e árvores procedurais e restaurar o layout/HUD originais; concluir apenas após DEVICE Android e paridade visual G-C3.

Reuso: `product-specific` ao Urbe; nenhuma extração para plataforma sem segundo consumidor (NN-022).
