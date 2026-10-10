using System.Numerics;
using Box2D.NET;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Joints;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace Lunet.Physics;

/// <summary>Tipo de movimento de um corpo físico.</summary>
public enum PhysicsBodyType
{
    /// <summary>Corpo imóvel, massa zero.</summary>
    Static,
    /// <summary>Corpo movido pelo jogo, não pela gravidade.</summary>
    Kinematic,
    /// <summary>Corpo com massa e dinâmica simulada.</summary>
    Dynamic
}

/// <summary>Geometria imutável de círculo ou caixa, em unidades do mundo físico.</summary>
/// <example><code>
/// var shape = Lunet.Physics.Collider2D.Circle(0.5f);
/// </code></example>
public readonly struct Collider2D
{
    /// <summary>Verdadeiro para círculo; falso para caixa.</summary>
    public bool IsCircle { get; }
    /// <summary>Raio para círculos, zero para caixas.</summary>
    public float Radius { get; }
    /// <summary>Metade da largura/altura para caixas.</summary>
    public Vector2 HalfExtents { get; }

    private Collider2D(bool isCircle, float radius, Vector2 halfExtents)
    { IsCircle = isCircle; Radius = radius; HalfExtents = halfExtents; }

    /// <summary>Cria uma forma circular com raio positivo.</summary>
    /// <param name="radius">Raio em metros do jogo.</param>
    /// <returns>Colisor circular validado.</returns>
    public static Collider2D Circle(float radius)
    {
        CheckPositive(radius, nameof(radius));
        return new Collider2D(true, radius, default);
    }

    /// <summary>Cria caixa com largura e altura positivas.</summary>
    /// <param name="width">Largura total.</param>
    /// <param name="height">Altura total.</param>
    /// <returns>Colisor retangular validado.</returns>
    public static Collider2D Box(float width, float height)
    {
        CheckPositive(width, nameof(width)); CheckPositive(height, nameof(height));
        return new Collider2D(false, 0f, new Vector2(width * 0.5f, height * 0.5f));
    }

    internal static void CheckPositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(name);
    }
    internal static B2Vec2 ToB2(Vector2 v) => new(v.X, v.Y);
    internal static Vector2 FromB2(B2Vec2 v) => new(v.X, v.Y);
    internal static void CheckFinite(Vector2 v, string name)
    {
        if (!float.IsFinite(v.X) || !float.IsFinite(v.Y)) throw new ArgumentOutOfRangeException(name);
    }
}

/// <summary>Fixture física pertencente a um corpo, com material e sensor opcionais.</summary>
/// <example><code>
/// using var world = new Lunet.Physics.PhysicsWorld(System.Numerics.Vector2.Zero);
/// var body = world.CreateBody(Lunet.Physics.PhysicsBodyType.Static, System.Numerics.Vector2.Zero);
/// var fixture = body.AddFixture(Lunet.Physics.Collider2D.Circle(1));
/// </code></example>
public sealed class Fixture2D
{
    internal readonly B2ShapeId Id;
    internal readonly PhysicsWorld World;
    internal Fixture2D(PhysicsWorld world, B2ShapeId id) { World = world; Id = id; }

    /// <summary>Verdadeiro enquanto a fixture e o mundo existem.</summary>
    public bool IsValid => !World.IsDisposed && b2Shape_IsValid(Id);
}

/// <summary>Corpo rígido de Box2D oculto atrás da API portátil do Lunet.</summary>
/// <example><code>
/// using var world = new Lunet.Physics.PhysicsWorld(System.Numerics.Vector2.Zero);
/// var body = world.CreateBody(Lunet.Physics.PhysicsBodyType.Dynamic, System.Numerics.Vector2.Zero);
/// body.AddFixture(Lunet.Physics.Collider2D.Circle(1));
/// </code></example>
public sealed class RigidBody2D
{
    internal readonly B2BodyId Id;
    internal readonly PhysicsWorld World;
    internal RigidBody2D(PhysicsWorld world, B2BodyId id) { World = world; Id = id; }

    private void EnsureAlive()
    {
        World.EnsureAlive();
        if (!b2Body_IsValid(Id)) throw new ObjectDisposedException(nameof(RigidBody2D));
    }

    /// <summary>Posição do centro/origem do corpo.</summary>
    public Vector2 Position { get { EnsureAlive(); return Collider2D.FromB2(b2Body_GetPosition(Id)); } }
    /// <summary>Velocidade linear no sistema de coordenadas do mundo.</summary>
    public Vector2 LinearVelocity
    {
        get { EnsureAlive(); return Collider2D.FromB2(b2Body_GetLinearVelocity(Id)); }
        set { Collider2D.CheckFinite(value, nameof(value)); EnsureAlive(); b2Body_SetLinearVelocity(Id, Collider2D.ToB2(value)); }
    }
    /// <summary>Massa calculada das fixtures não sensoriais.</summary>
    public float Mass { get { EnsureAlive(); return b2Body_GetMass(Id); } }
    /// <summary>Adiciona forma, densidade, atrito e restituição.</summary>
    /// <param name="collider">Forma circular ou retangular.</param>
    /// <param name="density">Massa por área; não negativa.</param>
    /// <param name="friction">Atrito não negativo.</param>
    /// <param name="restitution">Rebote no intervalo de zero a um.</param>
    /// <param name="sensor">Quando true detecta contato, sem resposta física.</param>
    /// <returns>Fixture pertencente ao corpo.</returns>
    public Fixture2D AddFixture(Collider2D collider, float density = 1f, float friction = 0.4f, float restitution = 0f, bool sensor = false)
    {
        EnsureAlive();
        if (!float.IsFinite(density) || density < 0) throw new ArgumentOutOfRangeException(nameof(density));
        if (!float.IsFinite(friction) || friction < 0) throw new ArgumentOutOfRangeException(nameof(friction));
        if (!float.IsFinite(restitution) || restitution is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(restitution));
        if (collider.IsCircle && collider.Radius <= 0 || !collider.IsCircle &&
            (collider.HalfExtents.X <= 0 || collider.HalfExtents.Y <= 0)) throw new ArgumentException("Collider inválido.", nameof(collider));
        var def = b2DefaultShapeDef();
        def.density = density;
        def.isSensor = sensor;
        def.enableContactEvents = true;
        def.enableSensorEvents = true;
        def.material.friction = friction;
        def.material.restitution = restitution;
        B2ShapeId id;
        if (collider.IsCircle)
        {
            var circle = new B2Circle(new B2Vec2(0, 0), collider.Radius);
            id = b2CreateCircleShape(Id, def, circle);
        }
        else
        {
            var box = b2MakeBox(collider.HalfExtents.X, collider.HalfExtents.Y);
            id = b2CreatePolygonShape(Id, def, box);
        }
        var fixture = new Fixture2D(World, id);
        World.Register(fixture);
        return fixture;
    }

    /// <summary>Aplica impulso instantâneo ao centro do corpo.</summary>
    /// <param name="impulse">Impulso em unidades físicas.</param>
    public void ApplyImpulse(Vector2 impulse)
    {
        Collider2D.CheckFinite(impulse, nameof(impulse));
        EnsureAlive(); b2Body_ApplyLinearImpulseToCenter(Id, Collider2D.ToB2(impulse), true);
    }
    /// <summary>Remove este corpo, as fixtures e as juntas conectadas.</summary>
    public void Destroy() { EnsureAlive(); World.RemoveBody(this); }
}

/// <summary>Junta de distância entre dois corpos; destruída com o mundo ou um corpo conectado.</summary>
/// <example><code>
/// using var world = new Lunet.Physics.PhysicsWorld(System.Numerics.Vector2.Zero);
/// var a = world.CreateBody(Lunet.Physics.PhysicsBodyType.Dynamic, System.Numerics.Vector2.Zero);
/// var b = world.CreateBody(Lunet.Physics.PhysicsBodyType.Dynamic, new System.Numerics.Vector2(2, 0));
/// a.AddFixture(Lunet.Physics.Collider2D.Circle(1)); b.AddFixture(Lunet.Physics.Collider2D.Circle(1));
/// var joint = world.CreateDistanceJoint(a, b, 2);
/// </code></example>
public sealed class Joint2D
{
    internal readonly B2JointId Id;
    internal readonly PhysicsWorld World;
    internal Joint2D(PhysicsWorld world, B2JointId id) { World = world; Id = id; }
    /// <summary>Verdadeiro enquanto a junta continua registrada no backend.</summary>
    public bool IsValid => !World.IsDisposed && b2Joint_IsValid(Id);
    /// <summary>Remove a junta sem remover os corpos.</summary>
    public void Destroy()
    {
        World.EnsureAlive();
        if (!b2Joint_IsValid(Id)) throw new ObjectDisposedException(nameof(Joint2D));
        b2DestroyJoint(Id, true);
    }
}

/// <summary>Contato iniciado durante o último passo entre duas fixtures.</summary>
/// <example><code>
/// Lunet.Physics.Contact contact = default;
/// var fixture = contact.A;
/// </code></example>
public readonly struct Contact
{
    /// <summary>Primeira fixture em contato.</summary>
    public Fixture2D A { get; }
    /// <summary>Segunda fixture em contato.</summary>
    public Fixture2D B { get; }
    internal Contact(Fixture2D a, Fixture2D b) { A = a; B = b; }
}

/// <summary>Resultado do primeiro obstáculo encontrado por um raycast.</summary>
/// <example><code>
/// Lunet.Physics.RaycastHit ray = default;
/// var point = ray.Point;
/// </code></example>
public readonly struct RaycastHit
{
    /// <summary>Fixture interceptada.</summary>
    public Fixture2D Fixture { get; }
    /// <summary>Ponto no mundo.</summary>
    public Vector2 Point { get; }
    /// <summary>Normal de superfície.</summary>
    public Vector2 Normal { get; }
    /// <summary>Distância percorrida pelo raio.</summary>
    public float Distance { get; }
    internal RaycastHit(Fixture2D fixture, Vector2 point, Vector2 normal, float distance)
    { Fixture = fixture; Point = point; Normal = normal; Distance = distance; }
}

/// <summary>Mundo físico Box2D em C# gerenciado, independente de Android e do renderizador.</summary>
/// <remarks>Use metros/unidades consistentes, um Step por Update fixo e descarte com Dispose.
/// Os contatos de início pertencem ao último Step e devem ser consumidos antes do próximo.</remarks>
/// <example><code>
/// using var world = new Lunet.Physics.PhysicsWorld(new System.Numerics.Vector2(0, 9.8f));
/// var ball = world.CreateBody(Lunet.Physics.PhysicsBodyType.Dynamic, new System.Numerics.Vector2(0, 0));
/// ball.AddFixture(Lunet.Physics.Collider2D.Circle(0.5f));
/// world.Step(1f / 60f);
/// </code></example>
public sealed class PhysicsWorld : IDisposable
{
    private readonly B2WorldId id;
    private readonly List<RigidBody2D> bodies = new();
    private readonly Dictionary<B2ShapeId, Fixture2D> fixtures = new();
    private readonly List<Contact> contactBegins = new();

    /// <summary>Verdadeiro após Dispose; objetos do mundo deixam de ser válidos.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Número de corpos ainda existentes no mundo.</summary>
    public int BodyCount { get { EnsureAlive(); return bodies.Count; } }
    /// <summary>Quantidade de pares cujo contato começou no último Step.</summary>
    public int ContactBeginCount { get { EnsureAlive(); return contactBegins.Count; } }
    /// <summary>Gravidade do mundo.</summary>
    public Vector2 Gravity
    {
        get { EnsureAlive(); return Collider2D.FromB2(b2World_GetGravity(id)); }
        set { Collider2D.CheckFinite(value, nameof(value)); EnsureAlive(); b2World_SetGravity(id, Collider2D.ToB2(value)); }
    }

    /// <summary>Cria mundo determinístico de thread única, com gravidade informada.</summary>
    /// <param name="gravity">Gravidade em unidades por segundo ao quadrado.</param>
    public PhysicsWorld(Vector2 gravity)
    {
        Collider2D.CheckFinite(gravity, nameof(gravity));
        var def = b2DefaultWorldDef();
        def.gravity = Collider2D.ToB2(gravity);
        def.workerCount = 1;
        id = b2CreateWorld(def);
    }

    internal void EnsureAlive()
    { if (IsDisposed) throw new ObjectDisposedException(nameof(PhysicsWorld)); }
    internal void Register(Fixture2D fixture) => fixtures.Add(fixture.Id, fixture);

    /// <summary>Cria corpo estático, cinemático ou dinâmico na posição informada.</summary>
    /// <param name="type">Tipo de movimento.</param>
    /// <param name="position">Origem do corpo.</param>
    /// <returns>Corpo recém-criado.</returns>
    public RigidBody2D CreateBody(PhysicsBodyType type, Vector2 position)
    {
        EnsureAlive(); Collider2D.CheckFinite(position, nameof(position));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        var def = b2DefaultBodyDef();
        def.position = Collider2D.ToB2(position);
        def.type = type switch {
            PhysicsBodyType.Dynamic => B2BodyType.b2_dynamicBody,
            PhysicsBodyType.Kinematic => B2BodyType.b2_kinematicBody,
            _ => B2BodyType.b2_staticBody
        };
        var body = new RigidBody2D(this, b2CreateBody(id, def));
        bodies.Add(body);
        return body;
    }

    internal void RemoveBody(RigidBody2D body)
    {
        // IDs das fixtures são invalidados pelo backend; limpe o mapa antes de reutilizá-los.
        int count = b2Body_GetShapeCount(body.Id);
        if (count > 0)
        {
            var ids = new B2ShapeId[count];
            b2Body_GetShapes(body.Id, ids, count);
            for (int i = 0; i < count; i++) fixtures.Remove(ids[i]);
        }
        b2DestroyBody(body.Id);
        bodies.Remove(body);
        contactBegins.Clear();
    }

    /// <summary>Une dois corpos deste mundo por uma distância fixa entre suas origens.</summary>
    /// <param name="a">Primeiro corpo.</param>
    /// <param name="b">Segundo corpo.</param>
    /// <param name="length">Distância positiva.</param>
    /// <returns>Junta ativa entre os corpos.</returns>
    public Joint2D CreateDistanceJoint(RigidBody2D a, RigidBody2D b, float length)
    {
        ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b);
        EnsureAlive(); Collider2D.CheckPositive(length, nameof(length));
        if (!ReferenceEquals(a.World, this) || !ReferenceEquals(b.World, this) || ReferenceEquals(a,b))
            throw new ArgumentException("Os corpos devem ser distintos e pertencer ao mesmo mundo.");
        if (!b2Body_IsValid(a.Id) || !b2Body_IsValid(b.Id))
            throw new ObjectDisposedException(nameof(RigidBody2D));
        var def = b2DefaultDistanceJointDef();
        def.@base.bodyIdA = a.Id; def.@base.bodyIdB = b.Id;
        def.length = length;
        return new Joint2D(this, b2CreateDistanceJoint(id, def));
    }

    /// <summary>Avança a simulação; recomendação: delta fixo de 1/60 e quatro subpassos.</summary>
    /// <param name="deltaSeconds">Tempo estritamente positivo.</param>
    /// <param name="subSteps">Subpassos positivos.</param>
    public void Step(float deltaSeconds, int subSteps = 4)
    {
        EnsureAlive(); Collider2D.CheckPositive(deltaSeconds, nameof(deltaSeconds));
        if (subSteps is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(subSteps));
        b2World_Step(id, deltaSeconds, subSteps);
        contactBegins.Clear();
        var events = b2World_GetContactEvents(id);
        for (int i = 0; i < events.beginCount; i++)
        {
            var begin = events.beginEvents[i];
            if (fixtures.TryGetValue(begin.shapeIdA, out var a) && fixtures.TryGetValue(begin.shapeIdB, out var b))
                contactBegins.Add(new Contact(a, b));
        }
    }

    /// <summary>Copia contatos iniciados no último Step, sem alocar no destino.</summary>
    /// <param name="destination">Buffer de contatos.</param>
    /// <returns>Quantidade de contatos copiados.</returns>
    public int CopyContactBegins(Span<Contact> destination)
    {
        EnsureAlive();
        int count = Math.Min(destination.Length, contactBegins.Count);
        for (int i = 0; i < count; i++) destination[i] = contactBegins[i];
        return count;
    }

    /// <summary>Consulta o primeiro objeto interceptado por um raio de comprimento limitado.</summary>
    /// <param name="origin">Origem do raio.</param>
    /// <param name="direction">Direção não nula; normalizada internamente.</param>
    /// <param name="distance">Alcance positivo.</param>
    /// <param name="hit">Resultado quando encontra um objeto.</param>
    /// <returns>True quando uma fixture é atingida.</returns>
    public bool Raycast(Vector2 origin, Vector2 direction, float distance, out RaycastHit hit)
    {
        EnsureAlive();
        Collider2D.CheckFinite(origin, nameof(origin));
        Collider2D.CheckFinite(direction, nameof(direction));
        Collider2D.CheckPositive(distance, nameof(distance));
        if (direction.LengthSquared() < 1e-12f) throw new ArgumentOutOfRangeException(nameof(direction));
        var ray = Vector2.Normalize(direction) * distance;
        var result = b2World_CastRayClosest(id, Collider2D.ToB2(origin), Collider2D.ToB2(ray), b2DefaultQueryFilter());
        if (result.hit && fixtures.TryGetValue(result.shapeId, out var fixture))
        {
            hit = new RaycastHit(fixture, Collider2D.FromB2(result.point), Collider2D.FromB2(result.normal), result.fraction * distance);
            return true;
        }
        hit = default;
        return false;
    }

    /// <summary>Destrói todos os recursos físicos de forma idempotente.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        b2DestroyWorld(id);
        bodies.Clear(); fixtures.Clear(); contactBegins.Clear();
    }
}
