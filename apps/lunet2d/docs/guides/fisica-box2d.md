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

## Física avançada (Fase 4)

```csharp
var sensor = floor.AddFixture(Collider2D.Box(4, 1), sensor: true);
sensor.SetCollisionFilter(categoryBits: 2, maskBits: 1);
var capsule = ball.AddFixture(Collider2D.Capsule(0.2f, 1f));
var hinge = world.CreateRevoluteJoint(floor, ball, Vector2.Zero);
var guide = world.CreatePrismaticJoint(floor, ball, Vector2.Zero, Vector2.UnitX);
Span<Contact> contacts = stackalloc Contact[8];
int starts = world.CopySensorBegins(contacts);
int finishes = world.CopySensorEnds(contacts);
int physicalEnds = world.CopyContactEnds(contacts);
world.RaycastFiltered(Vector2.Zero, Vector2.UnitX, 10, categoryBits: 1, maskBits: 2, out var target);
```

Os eventos pertencem apenas ao último passo e são descartados em destruição manual de fixture/corpo; IDs inválidos do backend são ignorados. Para distinguir um sensor, use `Fixture2D.IsSensor`. Juntas ancoradas em posições do mundo usam posições locais calculadas em cada corpo. Dispositivos Android ainda precisam de teste visual e de frame pacing.
