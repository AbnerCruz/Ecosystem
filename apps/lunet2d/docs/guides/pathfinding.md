# Rotas com A*

`Lunet.Pathfinding.GridPathfinder` encontra rotas de menor custo em uma grade finita, offline, sem ECS ou Tile Studio obrigatório. Cada célula tem custo de entrada: zero é parede; valores positivos finitos representam terreno, inclusive custos menores que 1. A origem não cobra custo. Quatro vizinhos são o padrão; oito vizinhos permitem diagonais com distância √2, sem passar entre paredes. Uma diagonal exige ambas as células laterais livres.

Crie a grade e um `GridPoint[]` reutilizável. `FindPath` recebe o buffer como `Span<GridPoint>` e devolve `PathResult`:

- `Found`: use somente `path.AsSpan(0, result.Length)`, da origem ao destino inclusive. Células posteriores são preservadas.
- `NotFound`: extremos bloqueados ou destino inacessível; comprimento zero e custo infinito; saída preservada.
- `BufferTooSmall`: há uma rota, mas faltou espaço; `Length` informa o tamanho necessário e `Cost` o custo. Saída preservada, sem rota parcial. Repita com um buffer maior.

Coordenadas fora da grade e custos negativos/NaN/infinito lançam exceção. `SetCost` muda buscas futuras, sem atualizar uma rota já copiada. O desempate é estável na mesma grade e opções. A conversão entre células e posições do mundo, movimento de agentes e atualização dos obstáculos pertencem ao jogo; esta API não promete desvio de agentes móveis ou movimento físico.

Construção aloca seis arrays, aproximadamente 32 bytes por célula, além dos objetos. Cada busca limpa os buffers e examina o menor custo positivo em O(células); a fila é um heap binário reutilizável. Buscas válidas não alocam, inclusive quando não há rota ou a saída é insuficiente. A execução é síncrona e pode ocupar o frame em grades grandes; não busque a cada Draw. Instâncias não são thread-safe nem reentrantes. A API não define um formato persistente de mapa nem importa o futuro Tile Studio.

## Exemplo no Preview

Crie um projeto **Em branco** separado e substitua a classe do jogo por este código. A grade tem 10 × 14 células de 32 pixels virtuais, abaixo do cabeçalho. Uma parede vertical tem passagem na linha 9; terreno azul custa 5 vezes mais. Toque em uma célula para calcular a rota a partir do marcador amarelo; toque no cabeçalho para alternar quatro/oito vizinhos.

```csharp
using System;
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.Pathfinding;

public sealed class PathfindingDemo : Game
{
    readonly GridPathfinder grid = new(10, 14);
    readonly GridPoint[] path = new GridPoint[140];
    readonly GridPoint start = new(1, 1);
    GridPoint goal = new(8, 1);
    PathResult result;
    bool diagonals;
    SpriteBatch batch = null!;
    SpriteFont font = null!;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        for (int y = 0; y < 14; y++)
            if (y != 9) grid.SetCost(new(4, y), 0);
        for (int y = 0; y < 8; y++) grid.SetCost(new(7, y), 5);
        Search();
    }

    void Search() => result = grid.FindPath(start, goal, path, diagonals);
    Vector2 Center(GridPoint cell) => new(20 + cell.X * 32 + 16, 100 + cell.Y * 32 + 16);

    protected override void Update(GameTime time)
    {
        if (!Input.Pointer.WasPressed || !Input.TryGetPointer(out var touch)) return;
        if (touch.Y < 80) { diagonals = !diagonals; Search(); return; }
        int x = (int)MathF.Floor((touch.X - 20) / 32);
        int y = (int)MathF.Floor((touch.Y - 100) / 32);
        if (x < 0 || x >= grid.Width || y < 0 || y >= grid.Height) return;
        goal = new(x, y);
        Search();
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
        batch.Begin();
        batch.DrawString(font, diagonals ? "8 vizinhos - toque para 4" : "4 vizinhos - toque para 8", new(12, 15), Color.White);
        batch.DrawString(font, result.Status == PathStatus.Found ? "Rota: " + (result.Length - 1) + " passos" : "Sem rota", new(12, 45), Color.White);
        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width; x++)
        {
            var cell = new GridPoint(x, y);
            float cost = grid.GetCost(cell);
            var rect = new RectangleF(20 + x * 32, 100 + y * 32, 30, 30);
            batch.FillRect(rect, cost == 0 ? Color.White : cost > 1 ? Color.CornflowerBlue : new Color(35, 35, 35));
        }
        if (result.Status == PathStatus.Found)
            for (int i = 1; i < result.Length; i++) batch.Line(Center(path[i - 1]), Center(path[i]), Color.Yellow, 3);
        batch.Circle(Center(start), 8, Color.Yellow, 3);
        batch.Circle(Center(goal), 10, Color.Red, 3);
        batch.End();
    }
}
```

1. Execute offline. A rota amarela passa pela única abertura na parede branca; não atravessa paredes.
2. Toque em destinos nos dois lados; a rota parte sempre do círculo amarelo. O círculo vermelho mostra o destino.
3. Toque na parede: aparece **Sem rota**, sem reaproveitar a linha anterior. Toque no ponto inicial: zero passos.
4. Alterne o cabeçalho entre quatro/oito vizinhos. Diagonais aparecem sem atravessar os cantos da parede; terreno azul influencia a escolha.
5. Teste bordas, área fora da grade, Preview rápido/isolado e um projeto anterior. A grade deve continuar alinhada ao toque com letterbox e rotação do telefone.

Roteiro intermediário LUNET-406: build e resultado no aparelho são evidências separadas dos testes portáteis. O texto do HUD aloca ao desenhar; a garantia de zero alocação aplica-se à API de busca, não ao jogo completo. Integração e gate da Fase 4 não são aprovados por este roteiro automaticamente.
