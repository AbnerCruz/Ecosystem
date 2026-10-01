using System.Numerics;
using Lunet.Graphics;
using Lunet.Runtime.Inspection;

namespace Lunet.Tests;

public class InspectorTests
{
    public enum Mode { Easy, Hard }

    public struct Stats
    {
        public int Lives;
        public float Speed;
    }

    public sealed class Enemy
    {
        public string Name = "orc";
        public int Hp = 10;
    }

    public sealed class Sample
    {
        public int Score = 3;
        [Range(0, 10)] public float Volume = 5;
        public bool Paused;
        public string Title = "Jogo";
        [Multiline(3)] public string Notes = "a\nb";
        [Color] public string Tint = "#FF0000";
        [File(".png")] public string Sprite = "hero.png";
        [Asset(AssetKind.Sound)] public string Jump = "jump.wav";
        public Mode Difficulty = Mode.Hard;
        public Vector2 Position = new(1, 2);
        public Color Background = new(1, 2, 3);
        [ReadOnly] public int Frame = 9;
        [Hidden] public int Secret = 1;
        [Inspect] private int _hidden = 42;
        [Group("Combate"), Tooltip("dano base")] public int Damage = 4;
        public Stats Player = new() { Lives = 3, Speed = 1.5f };
        public Enemy Boss = new();
        public Enemy? Missing;
        public List<int> Scores = [10, 20, 30];
        public int ReadOnlyProperty => 5;
        public int Writable { get; set; } = 7;
        public static int Ignored = 1;
        public int PrivateHidden() => _hidden;
    }

    private static InspectorItem Find(IReadOnlyList<InspectorItem> items, string label) => items.First(i => i.Label == label);

    [Fact]
    public void Reflection_ListsPublicMembersWithTheRightEditors_AndAttributes()
    {
        var items = ObjectInspector.Build(new Sample());
        Assert.Equal(InspectorItemKind.Header, items[0].Kind);
        Assert.Equal("Sample", items[0].Label);

        Assert.Equal(InspectorFieldKind.Int, Find(items, "Score").FieldKind);
        var volume = Find(items, "Volume");
        Assert.Equal((0f, 10f), (volume.Min, volume.Max));
        Assert.Equal(InspectorFieldKind.Bool, Find(items, "Paused").FieldKind);
        Assert.Equal(3, Find(items, "Notes").MultilineLines);
        Assert.Equal(InspectorFieldKind.Color, Find(items, "Tint").FieldKind);
        Assert.Equal(".png", Find(items, "Sprite").FileFilter);
        Assert.Equal(AssetKind.Sound, Find(items, "Jump").Asset);
        Assert.Equal(["Easy", "Hard"], Find(items, "Difficulty").EnumNames);
        Assert.Equal(InspectorFieldKind.Vector2, Find(items, "Position").FieldKind);
        Assert.Equal(InspectorFieldKind.Color, Find(items, "Background").FieldKind);

        Assert.False(Find(items, "Frame").IsEditable);
        Assert.False(Find(items, "ReadOnlyProperty").IsEditable);
        Assert.True(Find(items, "Writable").IsEditable);
        Assert.DoesNotContain(items, i => i.Label == "Secret");
        Assert.DoesNotContain(items, i => i.Label == "Ignored");
        Assert.Contains(items, i => i.Label == "_hidden");
        Assert.Equal("dano base", Find(items, "Damage").Tooltip);
        Assert.Contains(items, i => i is { Kind: InspectorItemKind.Header, Label: "Combate" });
    }

    [Fact]
    public void Setters_WriteThroughToTheObject_WithRangeClamping_AndEnumsAndStructs()
    {
        var sample = new Sample();
        var items = ObjectInspector.Build(sample);

        Find(items, "Score").Setter!(42);
        Assert.Equal(42, sample.Score);
        Find(items, "Volume").Setter!(99f);
        Assert.Equal(10f, sample.Volume);
        Find(items, "Difficulty").Setter!(Mode.Easy);
        Assert.Equal(Mode.Easy, sample.Difficulty);
        Find(items, "Position").Setter!(new Vector2(7, 8));
        Assert.Equal(new Vector2(7, 8), sample.Position);
        Find(items, "Lives").Setter!(9); // dentro da struct Player
        Assert.Equal(9, sample.Player.Lives);
        Find(items, "Hp").Setter!(1);
        Assert.Equal(1, sample.Boss.Hp);
        Find(items, "Score").Setter!(7L); // tipos diferentes são convertidos
        Assert.Equal(7, sample.Score);
        Assert.Equal(7, Find(items, "Score").Getter!());
        Find(items, "_hidden").Setter!(5);
        Assert.Equal(5, sample.PrivateHidden());
    }

    [Fact]
    public void NestedObjects_Lists_AndNulls_AreDescribed()
    {
        var sample = new Sample();
        var items = ObjectInspector.Build(sample);
        Assert.Contains(items, i => i is { Kind: InspectorItemKind.Header, Label: "Boss" });
        Assert.Equal(1, Find(items, "Name").Depth);
        Assert.Contains(items, i => i.Label == "Missing: (nulo)");
        Assert.Contains(items, i => i is { Kind: InspectorItemKind.Header, Label: "Scores [3]" });
        var second = Find(items, "[1]");
        Assert.Equal(20, second.Getter!());
        second.Setter!(21);
        Assert.Equal(21, sample.Scores[1]);
    }

    [Fact]
    public void Cycles_AreNotFollowedForever()
    {
        var a = new Node();
        a.Next = new Node { Next = a };
        var items = ObjectInspector.Build(a);
        Assert.True(items.Count < 50);
    }

    public sealed class Node
    {
        public Node? Next;
        public int Value;
    }

    public sealed class Turret
    {
        public float Angle;
        public int Shots;
    }

    public sealed class TurretInspector : Inspector<Turret>
    {
        public override void OnInspect(InspectorContext ui, Turret target)
        {
            ui.Header("Torre");
            ui.Slider("Ângulo", () => target.Angle, v => target.Angle = v, 0, 360);
            ui.Field("Disparos", () => target.Shots);
            ui.Button("Atirar", () => target.Shots++);
        }
    }

    public sealed class Level
    {
        public Turret Gun = new();
    }

    [Fact]
    public void CustomInspectors_ReplaceTheAutomaticList()
    {
        var customs = ObjectInspector.FindCustomInspectors(typeof(InspectorTests).Assembly);
        Assert.Contains(customs, c => c.TargetType == typeof(Turret));

        var level = new Level();
        var items = ObjectInspector.Build(level, customs);
        Assert.Contains(items, i => i is { Kind: InspectorItemKind.Header, Label: "Torre", Depth: 1 });
        var angle = Find(items, "Ângulo");
        angle.Setter!(500f);
        Assert.Equal(360f, level.Gun.Angle);
        Assert.False(Find(items, "Disparos").IsEditable);
        Find(items, "Atirar").Action!();
        Assert.Equal(1, level.Gun.Shots);
    }

    [Fact]
    public void Values_FormatAndParseRoundTrip_AndRejectInvalidText()
    {
        Assert.Equal("1.5", InspectorValue.Format(typeof(float), 1.5f));
        Assert.Equal("#FF8800", InspectorValue.Format(typeof(Color), new Color(255, 136, 0)));
        Assert.Equal("#FF880080", InspectorValue.Format(typeof(Color), new Color(255, 136, 0, 128)));
        Assert.Equal("1, 2.5", InspectorValue.Format(typeof(Vector2), new Vector2(1, 2.5f)));

        Assert.True(InspectorValue.TryParse(typeof(float), "2,5", out var f));
        Assert.Equal(2.5f, f);
        Assert.True(InspectorValue.TryParse(typeof(Vector2), "(3; 4)", out var v));
        Assert.Equal(new Vector2(3, 4), v);
        Assert.True(InspectorValue.TryParse(typeof(Color), "#f80", out var c));
        Assert.Equal(new Color(255, 136, 0), c);
        Assert.True(InspectorValue.TryParse(typeof(Mode), "hard", out var e));
        Assert.Equal(Mode.Hard, e);
        Assert.True(InspectorValue.TryParse(typeof(bool), "true", out var b));
        Assert.Equal(true, b);

        Assert.False(InspectorValue.TryParse(typeof(int), "abc", out _));
        Assert.False(InspectorValue.TryParse(typeof(byte), "300", out _));
        Assert.False(InspectorValue.TryParse(typeof(Mode), "Extreme", out _));
        Assert.False(InspectorValue.TryParse(typeof(Color), "#12", out _));
        Assert.False(InspectorValue.TryParse(typeof(Vector2), "1", out _));
    }

    [Fact]
    public void GameInstances_ShowOnlyTheirOwnMembers_NotTheFrameworkBase()
    {
        var items = ObjectInspector.Build(new Arena());
        Assert.Contains(items, i => i.Label == "Coins");
        Assert.DoesNotContain(items, i => i.Label is "Services" or "Log" or "Configuration" or "Dispatcher");
    }

    public sealed class Arena : Game
    {
        public int Coins = 3;
    }
}
