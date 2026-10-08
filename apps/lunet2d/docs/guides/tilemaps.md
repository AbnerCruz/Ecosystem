# Tilemaps com camadas, colisão e A* — LUNET-415

`TileMap` é um recurso **local ao Lunet2D**: o mapa é JSON editável em `Content/Data/` e o tileset é PNG em `Content/Textures/`. Não depende do Hub, do Tile Studio nem de uma ferramenta externa; os projetos existentes continuam no formato atual.

O mapa v1 segue este modelo:

```json
{
  "version": 1, "texture": "Textures/terrain.png",
  "width": 3, "height": 2, "tileWidth": 16, "tileHeight": 16,
  "layers": [
    {"name": "ground", "tiles": [1,1,1,1,1,1]},
    {"name": "walls", "visible": true, "collision": true, "tiles": [0,2,0,0,2,0]}
  ]
}
```

`tiles` são IDs na ordem das linhas, começando em 0 para vazio e 1 para o tile superior esquerdo da imagem; IDs seguintes percorrem o tileset da esquerda para a direita. Camadas são desenhadas na ordem do arquivo. `visible: false` apenas oculta o desenho; `collision: true` bloqueia toda célula onde o ID for diferente de 0. Fora da grade também é bloqueado.

No `LoadContent`, execute `var map = Content.LoadTileMap("Data/level.json"); var tileset = Content.LoadTexture(map.TexturePath, TextureFilter.Point);` e uma única vez `map.CopyCollisionTo(grid);`. O método substitui os custos do A* (0 nas paredes e custo positivo nas livres), por isso atualize-o somente quando mudar o mapa. No `Draw` use `map.Draw(batch, tileset, visibleWorld, origin, Color.White)` entre `batch.Begin()` e `batch.End()`, com `visibleWorld` representando o retângulo visível em coordenadas de **mundo**. Use AABB conservadora se a câmera girar. Tiles fora da viewport não são submetidos ao backend.

## Demonstração imediata no Preview

Crie um projeto **Em branco** e substitua a classe principal por esta implementação. Não precisa importar nenhum arquivo: o tileset é gerado em memória para testar o parser, as camadas, os obstáculos e o A*.

```csharp
using System;
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.Pathfinding;

public sealed class TileMapDemo : Game
{
    readonly TileMap map = TileMap.Parse("""
        {
          "version":1, "texture":"Textures/generated.png",
          "width":6, "height":6, "tileWidth":48, "tileHeight":48,
          "layers":[
            {"name":"ground","tiles":[
              1,1,1,1,1,1, 1,1,1,1,1,1, 1,1,1,1,1,1,
              1,1,1,1,1,1, 1,1,1,1,1,1, 1,1,1,1,1,1]},
            {"name":"walls","collision":true,"tiles":[
              0,0,0,0,0,0, 0,0,0,2,0,0, 0,0,0,2,0,0,
              0,0,0,2,0,0, 0,0,0,0,0,0, 0,0,0,0,0,0]}
          ]
        }
        """);
    readonly GridPathfinder grid = new(6, 6);
    readonly GridPoint[] path = new GridPoint[36];
    readonly GridPoint start = new(0, 0);
    readonly Vector2 origin = new(28, 150);
    PathResult route;
    SpriteBatch batch = null!;
    Texture2D texture = null!;
    SpriteFont font = null!;

    Vector2 Center(GridPoint cell) => map.CellBounds(cell, origin).Center;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        var pixels = new byte[96 * 48 * 4];
        for (int y = 0; y < 48; y++)
        for (int x = 0; x < 96; x++)
        {
            int i = (y * 96 + x) * 4;
            bool wall = x >= 48;
            byte value = (byte)((x / 12 + y / 12) % 2 == 0 ? 95 : 115);
            pixels[i] = wall ? (byte)180 : value;
            pixels[i + 1] = wall ? (byte)45 : (byte)145;
            pixels[i + 2] = wall ? (byte)65 : (byte)105;
            pixels[i + 3] = 255;
        }
        texture = Texture2D.FromPixels(GraphicsDevice, 96, 48, pixels, TextureFilter.Point);
        map.CopyCollisionTo(grid);
        route = grid.FindPath(start, new GridPoint(5, 5), path);
    }

    protected override void Update(GameTime time)
    {
        if (!Input.Pointer.WasPressed || !Input.TryGetPointer(out var touch)) return;
        var cell = map.WorldToCell(new Vector2(touch.X, touch.Y), origin);
        if (map.IsBlocked(cell)) return;
        route = grid.FindPath(start, cell, path);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
        batch.Begin();
        batch.DrawString(font, "Toque para buscar caminho", new Vector2(20, 40), Color.White);
        map.Draw(batch, texture, new RectangleF(0, 0, 360, 640), origin, Color.White);
        if (route.Status == PathStatus.Found)
            for (int i = 1; i < route.Length; i++)
                batch.Line(Center(path[i - 1]), Center(path[i]), Color.Yellow, 4);
        batch.Circle(Center(start), 9, Color.Yellow, 3);
        batch.End();
    }
}
```

### Teste no celular

Execute offline; observe o chão esverdeado e a parede vermelha. Toque atrás da parede e confira a linha amarela desviando pelos caminhos livres. Toque numa parede ou fora do mapa: não deve travar. Teste também os cantos, Preview rápido/isolado e a reabertura de projeto anterior, para verificar regressões. O desenho automatizado é testável no backend em memória, mas desempenho, toque e OpenGL real continuam sujeitos à validação no Android.

Limites: JSON v1 local, 32 camadas, até 1.048.576 células, tiles de 1 a 4096 pixels, único tileset por mapa e grade fixa. Ainda não oferece edição visual, autotiling, propriedades arbitrárias, streaming/chunks ou colisão física contínua. O Tile Studio é um trabalho da Fase 5; o mapa pode ser usado hoje exclusivamente por código C#.
