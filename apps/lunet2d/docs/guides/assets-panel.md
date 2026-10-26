# Painel Assets — Lunet F5 / LUNET-446

O painel **Assets** fica na barra superior do workspace (e também em `⋯ → Ferramentas → Assets`). É uma janela nativa Android para navegar recursos locais do projeto sem sair do editor C#.

## Fonte de verdade

`ProjectAssetCatalog.Build(project.ListFiles())` produz uma **projeção somente leitura** a cada abertura. Nenhum novo índice, banco de dados, migração de projeto ou alteração no formato `lunet.json` é introduzido. Somente arquivos de `Content/` e `Assets/` entram no catálogo. `.lunet/`, `bin/`, `obj/`, pastas de build e arquivos C# ficam no Explorer.

- `*.sprite.json`: fonte do Sprite Studio; associa o PNG de mesmo nome, omitindo a duplicação visual. Sem PNG, mostra a fonte mas não inventa textura carregável.
- `*.tilemap.json`: mapa editável no Tile Studio, com preview simples do tileset e informações de dimensões/camadas; **não** representa um preview renderizado do mapa.
- Imagens PNG/JPG; áudio WAV/OGG/MP3; fontes TTF/OTF; demais JSON: vistos como categorias distintas.
- Busca por parte do nome ou caminho e filtros por categoria. Para evitar criar milhares de Views, exibe no máximo 250 linhas simultâneas; a busca percorre todo o conjunto.

## Ações

Toque num item para ver metadados. Sprites abrem no Sprite Studio, tilemaps no Tile Studio e outros JSON no editor. O painel mostra exemplos C# das APIs públicas `Content.LoadTexture`, `Content.LoadTileMap`, `Content.LoadSound`, `Content.LoadMusic`, `Content.LoadTrueTypeFont` e `Content.ReadText`, com botão para copiá-los para a área de transferência.

A pré-visualização de imagens usa BitmapFactory com amostragem e limites; prévia de áudio usa MediaPlayer nativo Android e é encerrada com o diálogo. Os assets permanecem no projeto local. O botão `+ Criar` abre os Studios já disponíveis, sem criar um editor paralelo.

## Teste no dispositivo (pendente)

1. Abra um projeto que contenha sprite `.sprite.json` e a PNG derivada, mapa `.tilemap.json`, WAV, JSON comum e PNG avulsa (por exemplo Bosque Lumi).
2. Abra **Assets**. Confirme categorias e que o sprite não duplica a PNG. Teste busca e filtros.
3. Abra o detalhe do sprite e use **Editar no Studio**; altere e salve sem perder frames. Volte para Assets e confira o arquivo.
4. Abra um mapa suportado pelo Tile Studio (editor atual limita 64 × 64), edite e salve. Mapas maiores podem ser executados mas não abertos por esse editor inicial.
5. Visualize PNG, reproduza áudio e feche; confirme que o som não continua depois do fechamento.
6. Copie um exemplo e use-o em `Game.cs` (ajustando nomes conforme necessário). Execute o preview.
7. Feche e reabra o aplicativo, verificando integridade do projeto e ausência de regressão do Explorer, Run e exportação ZIP.

A validação Android física ainda é necessária: CI verde comprova compilação/testes automatizados, não layout, playback ou ciclo de vida no aparelho.
