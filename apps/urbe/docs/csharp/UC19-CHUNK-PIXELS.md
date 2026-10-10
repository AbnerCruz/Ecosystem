# UC-19 / LEGACY-2 — Composição contínua dos chunks 1.8.4-beta

Referência normativa: `UC19-VISUAL-ACCEPTANCE.md`, `UC19-PROCEDURAL-CITY.md`, `ROADMAP.md`, requisito de não reformular a arte. Oráculo técnico: `src/world/pixel-art.js::chunkPixels` (SHA `d47cf2902e8aeb5ca2c529d2e4c9617fb71ca931`) e `src/world/terrain.js` (SHA `7d4a1e88b153a6d942f968572a0260d5154e42ef`).

## Incremento

`Urbe.Core.LegacyChunkPixels.Render` compõe, **em tempo de execução C#**, buffers RGBA de 256×256 pixels para cada chunk 16×16, usando:

- Bioma e elevação derivados de `LegacyTileSampler`/`LegacyTileChunks`, incluindo halo de um tile em cada borda.
- As 19 paletas e 4 variantes do gerador `LegacyWorldPixelTextures`, sem arquivos PNG pré-gerados.
- Sombreamento da elevação com gradiente noroeste e manchas de cor (ruído de valor) usando os mesmos seeds do JS.
- Interpolação de biomas deformada por ruído, ecótonos terra–terra, água × terra, espuma costeira e profundidade da água.
- Decoração original por tile/slot delegada a `LegacyVegetation.DecorAt` e `LegacyWorldGroundDecor.Paint`.

Nenhum formato de arquivo, contrato de vault, saída de release, UI, host, importação ou dados de usuário foi alterado. O buffer é uma etapa interna de renderização, não uma nova fonte de verdade; o renderer final consumirá pixels sob demanda.

## Testes e limites

`LegacyChunkPixelsTests` confere comprimento/alpha, pixels de textura em água profunda com sombreamento, coordenadas negativas, determinismo, diferenças entre chunks, transição água/terra, decoração por tile e proteção de entradas. **A equivalência pixel a pixel de todo o chunk ainda precisa de oráculo JS e screenshots do mesmo cenário**; os testes desta fatia não a certificam.

A Cidade atualmente exibida em `World.razor` ainda usa `CityTileLayout` 8×8 e não foi substituída nesta fatia: mudar a tela antes de validar a composição, câmera, árvores, ruas, cidadãos, camadas e posicionamento quebraria a ordem de LEGACY-2 a LEGACY-4. Nenhum PNG estático será tomado como arte-fonte; data URI eventual é somente transporte de pixels de C# ao MAUI WebView.

## Próxima ação

1. Executar o CI .NET e consistency no estado combinado da PR, corrigindo regressões.
2. Gerar corpus byte a byte diretamente de `pixel-art.js::chunkPixels`, cotejar com a saída C# em sementes/biomas/costas idênticos e corrigir discrepâncias, sem alterar o oráculo.
3. Conectar buffers/chunks contínuos à Cidade com viewport/câmera e cache limitado, sem perder cliques e escrita SAF nem substituir o JS como referência.
4. Integrar estradas e árvores procedurais e restaurar o layout/HUD originais; concluir apenas após DEVICE Android e paridade visual G-C3.

Reuso: `product-specific` ao Urbe; nenhuma extração para plataforma sem segundo consumidor (NN-022).
