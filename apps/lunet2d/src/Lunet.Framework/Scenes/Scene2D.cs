using Lunet.Graphics;

namespace Lunet.Scenes;

/// <summary>Organização opcional de entidades de um jogo, em ordem de inserção.</summary>
/// <remarks>
/// Chame Update/Draw explicitamente no Game. Não há ECS obrigatório, serialização, hierarquia,
/// propriedade de recursos gráficos ou callbacks de attach/dispose. A API é da thread do jogo.
/// Add/Remove/Clear (inclusive de componentes) são proibidos durante um percurso desta cena;
/// faça mudanças estruturais antes/depois. Flags e transforms podem mudar dentro dos callbacks.
/// Exceções propagam ao host; o bloqueio é liberado, sem rollback dos callbacks já executados.
/// </remarks>
/// <example><code>
/// var scene = new Lunet.Scenes.Scene2D();
/// var player = new Lunet.Scenes.Entity2D();
/// scene.Add(player);
/// scene.Update(time);
/// batch.Begin(); scene.Draw(batch, time); batch.End();
/// </code></example>
public sealed class Scene2D
{
    private readonly List<Entity2D> entities = new();
    private bool traversing;

    /// <summary>Número de entidades, inclusive desabilitadas/invisíveis.</summary>
    public int Count => entities.Count;

    /// <summary>Permite atualização. Desligar durante um callback interrompe o percurso restante.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Permite desenho. Desligar durante um callback interrompe o percurso restante.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Entidade no índice de inserção; acesso inválido lança exceção.</summary>
    /// <param name="index">Índice zero-based da entidade.</param>
    public Entity2D this[int index] => entities[index];

    /// <summary>Adiciona uma entidade sem cena. Rejeita null, duplicata e ownership concorrente.</summary>
    /// <param name="entity">Entidade desanexada a adicionar.</param>
    public void Add(Entity2D entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureMutable();
        if (entity.Scene is not null) throw new InvalidOperationException("A entidade já pertence a uma cena.");
        entities.Add(entity);
        entity.Scene = this;
    }

    /// <summary>Remove sem descartar recursos ou componentes. Retorna falso se não pertence à cena.</summary>
    /// <param name="entity">Entidade a desanexar.</param>
    /// <returns>True quando removida; false quando não pertence à cena.</returns>
    public bool Remove(Entity2D entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureMutable();
        if (!ReferenceEquals(entity.Scene, this)) return false;
        entities.Remove(entity);
        entity.Scene = null;
        return true;
    }

    /// <summary>Desanexa todas as entidades, preservando componentes e recursos do jogo.</summary>
    public void Clear()
    {
        EnsureMutable();
        for (int i = 0; i < entities.Count; i++) entities[i].Scene = null;
        entities.Clear();
    }

    /// <summary>Atualiza entidades/componentes habilitados em ordem de inserção; não aloca.</summary>
    /// <param name="time">Tempo do passo fornecido pelo jogo.</param>
    public void Update(GameTime time)
    {
        EnsureMutable();
        if (!IsEnabled) return;
        traversing = true;
        try
        {
            for (int i = 0; i < entities.Count && IsEnabled; i++) entities[i].Update(time);
        }
        finally { traversing = false; }
    }

    /// <summary>Desenha entidades/componentes visíveis em ordem de inserção; não aloca.</summary>
    /// <remarks>O jogo controla Begin/End, câmera, blend e clipping do SpriteBatch. IsEnabled não oculta.</remarks>
    /// <param name="batch">Lote preparado pelo jogo.</param>
    /// <param name="time">Tempo do desenho fornecido pelo jogo.</param>
    public void Draw(SpriteBatch batch, GameTime time)
    {
        ArgumentNullException.ThrowIfNull(batch);
        EnsureMutable();
        if (!IsVisible) return;
        traversing = true;
        try
        {
            for (int i = 0; i < entities.Count && IsVisible; i++) entities[i].Draw(batch, time);
        }
        finally { traversing = false; }
    }

    internal void EnsureMutable()
    {
        if (traversing) throw new InvalidOperationException("Mude a estrutura fora de Update/Draw da cena; percursos reentrantes são proibidos.");
    }
}

/// <summary>Entidade opcional com transform e componentes próprios, sem hierarquia implícita.</summary>
/// <remarks>Componentes são preservados ao remover a entidade da cena. Sem identidade persistente ou serialização.</remarks>
/// <example><code>
/// var entity = new Lunet.Scenes.Entity2D();
/// entity.Transform.Position = new System.Numerics.Vector2(50, 100);
/// var scene = new Lunet.Scenes.Scene2D(); scene.Add(entity);
/// </code></example>
public sealed class Entity2D
{
    private readonly List<Component2D> components = new();

    /// <summary>Cena atual; null quando desanexada.</summary>
    public Scene2D? Scene { get; internal set; }

    /// <summary>Transform livre do jogo; componentes decidem como utilizá-lo.</summary>
    public Transform2D Transform = Transform2D.Identity;

    /// <summary>Permite atualização dos componentes.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Permite desenho dos componentes, independentemente de IsEnabled.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Quantidade de componentes anexados.</summary>
    public int Count => components.Count;

    /// <summary>Componente na ordem de inserção.</summary>
    /// <param name="index">Índice zero-based do componente.</param>
    public Component2D this[int index] => components[index];

    /// <summary>Anexa componente sem entidade; rejeita null/duplicata/ownership concorrente.</summary>
    /// <param name="component">Componente desanexado a adicionar.</param>
    public void Add(Component2D component)
    {
        ArgumentNullException.ThrowIfNull(component);
        Scene?.EnsureMutable();
        if (component.Entity is not null) throw new InvalidOperationException("O componente já pertence a uma entidade.");
        components.Add(component);
        component.Entity = this;
    }

    /// <summary>Desanexa sem descartar recursos; falso se o componente não pertence à entidade.</summary>
    /// <param name="component">Componente a desanexar.</param>
    /// <returns>True quando removido; false quando não pertence à entidade.</returns>
    public bool Remove(Component2D component)
    {
        ArgumentNullException.ThrowIfNull(component);
        Scene?.EnsureMutable();
        if (!ReferenceEquals(component.Entity, this)) return false;
        for (int i = 0; i < components.Count; i++)
            if (ReferenceEquals(components[i], component)) { components.RemoveAt(i); break; }
        component.Entity = null;
        return true;
    }

    /// <summary>Desanexa todos os componentes sem descartá-los.</summary>
    public void Clear()
    {
        Scene?.EnsureMutable();
        for (int i = 0; i < components.Count; i++) components[i].Entity = null;
        components.Clear();
    }

    /// <summary>Primeiro componente do tipo solicitado (inclui derivados); null se ausente.</summary>
    /// <typeparam name="T">Tipo base ou concreto de componente procurado.</typeparam>
    /// <returns>Primeiro componente compatível; null se ausente.</returns>
    public T? Get<T>() where T : Component2D
    {
        for (int i = 0; i < components.Count; i++) if (components[i] is T result) return result;
        return null;
    }

    internal void Update(GameTime time)
    {
        for (int i = 0; i < components.Count && IsEnabled; i++)
        {
            if (Scene is { IsEnabled: false }) break;
            var component = components[i];
            if (component.IsEnabled) component.Update(time);
        }
    }

    internal void Draw(SpriteBatch batch, GameTime time)
    {
        for (int i = 0; i < components.Count && IsVisible; i++)
        {
            if (Scene is { IsVisible: false }) break;
            var component = components[i];
            if (component.IsVisible) component.Draw(batch, time);
        }
    }
}

/// <summary>Comportamento opcional implementado pelo jogo. Recursos e descarte pertencem ao jogo.</summary>
/// <example><code>
/// var entity = new Lunet.Scenes.Entity2D();
/// Lunet.Scenes.Component2D? first = entity.Get&lt;Lunet.Scenes.Component2D&gt;();
/// bool absent = first is null;
/// </code></example>
public abstract class Component2D
{
    /// <summary>Entidade atual; null quando desanexado.</summary>
    public Entity2D? Entity { get; internal set; }

    /// <summary>Permite receber Update.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Permite receber Draw, independentemente de IsEnabled.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Atualiza comportamento. Mudanças estruturais devem ocorrer fora do percurso.</summary>
    /// <param name="time">Tempo do passo fornecido pela cena.</param>
    public virtual void Update(GameTime time) { }

    /// <summary>Desenha dentro do Begin/End controlado pelo jogo.</summary>
    /// <param name="batch">Lote preparado pelo jogo.</param>
    /// <param name="time">Tempo do desenho fornecido pela cena.</param>
    public virtual void Draw(SpriteBatch batch, GameTime time) { }
}
