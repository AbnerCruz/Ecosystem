# UC-19 — Cidade C# com arte procedural em execução

## Regra de arquitetura

**Fonte da arte = algoritmos C# do Urbe 1.8.4-beta**, não PNGs empacotados. Os procedimentos de `LegacyWorldPixelTextures`, `LegacyWorldGroundDecor`, `LegacyWorldSprites` e `LegacyWorldBuildings` geram pixels em memória. `WorldArt` usa PNG como **transporte temporário em memória (data URI)** para o WebView MAUI, sem ficheiros PNG no repositório, sem dependência de JS no cliente e com cache de chave finita (4 texturas de chão, 3 variantes de copa, 4 tipos de telhado e decoração por coordenadas da grade).

O PR de sprites estáticos #423 foi **fechado sem merge por decisão do proprietário**. Nunca reabrir ou reintroduzir a arquitetura de sprites pré-gerados como substituto para o motor procedural.

## Integração concreta

Este PR é uma mudança **empilhada sobre a branch Android do PR #397**, não sobre `main`, pois apenas esse branch contém a Cidade clicável e o vault SAF beta. As quatro classes procedurais + seus testes foram trazidas sem mudanças, a partir do já integrado PR #421. Em `World.razor` os elementos visuais passam a usar `WorldArt` C#: base do mapa, terreno em quatro variantes, detalhes de flores, carvalhos e os edifícios `house`/`hall`. `urbe.css` remove as referências à pasta inexistente `world/v184`, mantendo regras de posicionamento, escala `image-rendering:pixelated`, zoom e toque. A UI mantém os comandos de abrir nota/bairro, criar, organizar e voltar ao Explorer.

Testes xUnit decodificam os PNGs **gerados em runtime** e conferem os bytes RGBA exatos contra cada procedimento Core (sem snapshots construídos manualmente); smoke Playwright confirma que a Cidade carrega URLs `data:image/png;base64` nos terrenos, copa e fundo, e que a navegação de volta funciona. A publicação para o WebView e a compilação Android/Windows são verificadas pelo CI; **não existe nova versão web pública**.

## Limites e aceitação

**Não declarar fidelidade total ao Urbe 1.8.4-beta ainda.** A lógica `WorldArt.TerrainClass`/grade 8×8 é a disposição transitória UC-19 do cliente beta, *não* o algoritmo original de relevo/biomas/chunks. A geração de edifícios C# usa rasterização de polígonos com centros de pixel e não reproduz ainda o antialias exato Canvas2D; por isso screenshots comparativos com a mesma cidade legada são gate obrigatório antes de distribuição. Também restam atmosfera, floresta, personagens e mundo contínuo.

Próximas frentes: portar `terrain.js` + `chunkPixels` para C# puro usando seed, altitude, água, bioma e transições orgânicas, integrar tilemap contínuo no mundo, otimizar buffers e validar orientação móvel e frames. Depois controlar paletas/editabilidade. Não alterar dados de vault nem executar migração histórica.

## Integração responsável

- PR subordinado à branch funcional Android #397 até decisão de integrar esse PR crítico. **Não aplicar merge manual nem rótulo de autorização**; o proprietário deve manter a autoridade de gates críticos.
- Após integração de #397 em main, atualizar base e eliminar o transporte duplicado das classes Core já presentes em main; nunca reverter o conteúdo Core do PR #421.
- Testes físicos de toque, zoom, FPS, screenshot, armazenamento Android (G-C3/G-C4) permanecem pendentes, mesmo com CI verde.
