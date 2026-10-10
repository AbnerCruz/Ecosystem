# Física Box2D no Lunet — Fase 4

A API está em `Lunet.Physics` e executa offline com Box2D.NET em C#.

```csharp
using System.Numerics;
using Lunet.Physics;

using var world = new PhysicsWorld(new Vector2(0, 9.8f));
var floor = world.CreateBody(PhysicsBodyType.Static, new Vector2(0, 6));
floor.AddFixture(Collider2D.Box(20, 1), friction: 0.8f);
var ball = world.CreateBody(PhysicsBodyType.Dynamic, Vector2.Zero);
ball.AddFixture(Collider2D.Circle(0.5f), density: 1, restitution: 0.2f);
for (int frame = 0; frame < 120; frame++) world.Step(1f / 60f);
Vector2 position = ball.Position;
world.Raycast(new Vector2(0, -2), Vector2.UnitY, 20, out var hit);
```

Um mundo não renderiza por conta própria: desenhe sprites na posição de cada corpo no `Game.Draw`. Chame `Step` somente no Update fixo, não no Draw. Use unidades consistentes (por exemplo, 1 metro físico = 32 pixels na cena) e descarte `PhysicsWorld` com o jogo. Consulte os eventos de início por `ContactBeginCount` / `CopyContactBegins` antes do próximo Step.

A API desta entrega é inicial, não fecha toda a física da Fase 4: faltam juntas avançadas, filtros, eventos de término/sensores e validação Android.
