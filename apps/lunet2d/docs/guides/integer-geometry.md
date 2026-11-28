# Point, Size e Vector2

## Uso
Projetos novos no perfil Amplo recebem aliases visíveis em Project/Imports.cs. Em jogos existentes, acrescente manualmente:

```csharp
global using Point = System.Drawing.Point;
global using Size = System.Drawing.Size;
```

Não importe System.Drawing inteiro: Color e RectangleF têm nomes concorrentes. O perfil Essencial permanece mínimo. Não há migração automática.

```csharp
Point position = new(100, 100);
Size size = new(32, 32);
RectangleF rect = new(position, size);
Transform2D transform = new(position.ToVector2());
transform.Scale = size.ToVector2();
```

Scale representa fatores de escala quando usado pela matriz Transform2D; se o jogo o usa como dimensões do retângulo, isso é uma escolha do jogo. Os campos continuam Vector2. Conversão explícita preserva significado dos componentes, mas int grandes podem perder precisão (float). Não há conversão implícita de Vector2 para inteiros nem arredondamento oculto.

## Decisão
O estudo de 24/11 recomenda tipos BCL, sem System.Drawing.Common, aliases seletivos e composição testada. A release 1000619 já referencia System.Drawing.Primitives e implementa Size nas quatro APIs de textura/resolução. O caso Coin.cs de 28/11 contém construtor incompleto e Point usado como dimensão: o tipo apropriado para dimensão é Size, sem proibir que Point seja usado como par de inteiros no código do jogo. A lacuna concreta é a ausência dos aliases/conversões e das combinações de RectangleF.

A extensão ToVector2 tem uma implementação por tipo. RectangleF aceita Vector2/Vector2, Point/Vector2, Vector2/Size, Point/Size. As novas sobrecargas recebem prioridade inferior ao construtor antigo para preservar new(...) e default; a combinação Point/Size tem prioridade menor que as mistas para resolver um único argumento tipado e o outro inferido. Contains(Point) usa a mesma regra de borda. Resize(Size) encaminha ao Resize(int,int), mantendo o tratamento anterior de dimensões inválidas. Não criar tipo novo, não mudar Transform2D, nem introduzir sobrecargas indiscriminadas.

Referências: [prioridade de sobrecargas](https://learn.microsoft.com/pt-br/dotnet/CSharp/language-reference/proposals/csharp-13.0/overload-resolution-priority), [Point](https://learn.microsoft.com/dotnet/api/system.drawing.point), [Size](https://learn.microsoft.com/dotnet/api/system.drawing.size).

## Verificação
- Testes unitários de componentes, precisão, negativos, bordas e equivalência.
- Compilador Roslyn real do Lunet: aliases, Color, RectangleF, chamadas antigas com new/default e chamadas tipadas.
- Smoke do APK standalone exercita os tipos. Build do smoke não equivale à execução no Android; CI ainda não confirmado. SDK local indisponível.
