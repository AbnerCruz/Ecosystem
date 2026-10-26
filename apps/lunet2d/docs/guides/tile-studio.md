# Tile Studio inicial — LUNET-441 (pré-beta)

Abra um projeto e toque em **Arte → Tilemaps → Criar tilemap**. Escolha dimensões do mapa (16×16, 32×32 ou 64×64 células) e tiles de 16, 32 ou 64 pixels. **Não é necessária nenhuma importação.** O Lunet cria automaticamente um atlas editável em `Content/Textures/tiles-*.sprite.json`, sua textura PNG em `Content/Textures/tiles-*.png` e um mapa em `Content/Data/map-*.tilemap.json`. Quatro tiles de cores planas vêm como placeholders para permitir pintar o mapa imediatamente, não como arte definitiva.

No editor, **Editar tileset** abre o editor de pixels nativo, com linhas destacadas delimitando cada tile. Use lápis, balde, formas, cores e desfazer/refazer para criar suas próprias imagens. Ao salvar, o PNG é atualizado automaticamente mantendo o JSON editável, e **← Mapa** retorna ao Tile Studio recarregando a textura. A edição de tilesets importados sem fonte `.sprite.json` continua fora deste incremento.

**Escolher tile** abre a paleta visual do PNG; toque em um tile e desenhe arrastando na grade. **Borracha** alterna entre ID 0 (vazio) e o último tile. Em **Camada**, altere a camada ativa. É possível criar, renomear, excluir, ordenar, ocultar e marcar colisão. Desfazer/refazer guarda gestos, não pixels isolados. Cada gesto é salvo no JSON do projeto (com gravação atômica), inclusive ao colocar o app em segundo plano; falhas exibem **Não salvo**.

O arquivo usa diretamente o contrato `TileMap` v1 do framework. A fonte textual é a autoridade: `version`, `texture`, `width`, `height`, `tileWidth`, `tileHeight`, `layers` com `name`, `visible`, `collision`, `tiles`. Camadas com colisão bloqueiam A*, mesmo quando ocultas. Nenhum `Game.cs` é reescrito e o runtime não depende do Studio. PNG é mantido separado em `Content/Textures`.

No código do jogo, use `Content.LoadTileMap("Data/map-….tilemap.json")`, `Content.LoadTexture(map.TexturePath)` e `map.Draw(...)`. Consulte [tilemaps.md](tilemaps.md) para o uso da API.

## Roteiro de aparelho (ainda pendente)

1. Em um projeto vazio e sem arquivos PNG, crie um tilemap 16 × 16, tiles 16 pixels. Confirme que mapa JSON e tileset JSON+PNG aparecem no Explorer.
2. Abra **Editar tileset**, altere um tile com uma cor visível, salve, retorne a **← Mapa**. Confirme que a paleta e o mapa exibem os novos pixels. Escolha dois tiles, pinte arrastando, use Borracha, Desfazer e Refazer; confira persistência após reabrir.
3. Adicione uma camada `paredes`, marque colisão, pinte uma célula, oculte a camada e salve. Abra o JSON no Editor; as propriedades e IDs devem permanecer.
4. Alterne para Código e reabra o mapa; confirme persistência ao fechar e ao enviar o app para segundo plano. Execute um projeto que carregue o mapa para verificar o mesmo JSON em runtime.
5. Teste um tileset com dimensões inválidas e um mapa com tile ID além da imagem; ambos devem ser rejeitados sem modificar os arquivos existentes.

**Limites:** edição nativa de tileset restrita aos atlases criados no Lunet (dimensões até 128×128, um frame); não cria desenhos elaborados automaticamente. Sem zoom/pan, autotiling, seleção de retângulo, pincéis avançados, preview de física/A* dentro do editor, propriedades arbitrárias ou edição de dimensões. PNG suportado até 2048 × 2048 e no máximo 1024 tiles; mapa inicial de 16 × 16 células e mapas existentes até 64 × 64 células nesta UI. O runtime continua suportando mapas maiores por JSON. O código ainda precisa passar por CI Android e validação real de toque/layout (NN-017). O APK do jogo é outra entrega prioritária, não implementada aqui.
