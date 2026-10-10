# UC-19 — Cidade C# com arte procedural em execução

## Regra de arquitetura

**Fonte da arte = algoritmos C# do Urbe 1.8.4-beta**, não PNGs empacotados. Os procedimentos de `LegacyWorldPixelTextures`, `LegacyWorldGroundDecor`, `LegacyWorldSprites` e `LegacyWorldBuildings` geram pixels em memória. `WorldArt` usa PNG como **transporte temporário em memória (data URI)** para o WebView MAUI, sem ficheiros PNG no repositório, sem dependência de JS no cliente e com cache de chave finita (4 texturas de chão, 3 variantes de copa, 4 tipos de telhado e decoração por coordenadas da grade).

O PR de sprites estáticos #423 foi **fechado sem merge por decisão do proprietário**. Nunca reabrir ou reintroduzir a arquitetura de sprites pré-gerados como substituto para o motor procedural.

## Integração concreta — situação consolidada

- PR #397 **integrado**: cliente C# Android com armazenamento físico SAF, Cidade clicável e grade transitória 8×8.
- PR #421 **integrado**: texturas/decorativos/sprites/construções procedurais em `Urbe.Core`.
- PR #425 **integrado**: a UI de `World.razor` recebe buffers RGBA de C#, codificados em PNG `data:` na memória; não depende dos PNGs estáticos ausentes `world/v184/*`. Os testes `WorldArtProceduralRenderTests` comprovam *pixels do gerador C#*, não equivalência visual de toda a Cidade.
- PR #423 **fechado sem merge**: sprites pré-gerados não correspondem à arquitetura aprovada. O PR #385 (Cidade SVG experimental) não define a estética da versão final.
- Este documento registra a **fatia integrada** e suas lacunas; a ordem de prioridade para próximos agentes permanece somente no `ROADMAP.md`, contrato de continuidade. O contrato de aceite é `UC19-VISUAL-ACCEPTANCE.md`.

## Limites e aceitação

**Não declarar fidelidade total ao Urbe 1.8.4-beta ainda.** A lógica `WorldArt.TerrainClass`/grade 8×8 é a disposição transitória UC-19 do cliente beta, *não* o algoritmo original de relevo/biomas/chunks. A geração de edifícios C# usa rasterização de polígonos com centros de pixel e não reproduz ainda o antialias exato Canvas2D; por isso screenshots comparativos com a mesma cidade legada são gate obrigatório antes de distribuição. Também restam atmosfera, floresta, personagens e mundo contínuo.

Próximas frentes: portar `terrain.js` + `chunkPixels` para C# puro usando seed, altitude, água, bioma e transições orgânicas, integrar tilemap contínuo no mundo, otimizar buffers e validar orientação móvel e frames. Depois controlar paletas/editabilidade. Não alterar dados de vault nem executar migração histórica.

## Próximos incrementos

Implementar a geração real de `terrain.js` + `chunkPixels` em C# com seed, elevação, água, biomas e transições; conectar mundo top-down contínuo, sprites, casas/bairros, ruas/wikilinks, câmera e toque à sessão do vault real. Restaurar HUD, tipografia e composição sem substituição artística. Otimizar alocação/frame e executar comparativo de screenshots no Android real. Nenhum incremento altera o vault só por estética.

## Integração responsável

Antes de criar novos PRs, verificar `main`, a fila de PRs e o CI em tempo real, recuperar testes/funcionalidades úteis e encerrar ramos obsoletos sem *force push*. Seguir o integrador automático e a política de autorização crítica para alterações de dados, governança ou release. A evidência automática dos PRs #397/#421/#425 não substitui toque, FPS, salvamento e fidelidade visual humana G-C3/G-C4.
