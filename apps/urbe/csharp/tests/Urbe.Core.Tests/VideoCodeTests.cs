using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>#1004: video drawn by free JavaScript the agent writes, run in Jint with nothing of the app in reach.</summary>
public sealed class VideoCodeTests
{
    [Fact]
    public void AProgramDrawsWithLoopsFunctionsAndTime()
    {
        const string code = """
            function orbit(i, t) { return { x: 100 + 50 * Math.cos(i + t), y: 100 + 50 * Math.sin(i + t) }; }
            function draw(ctx, t, info) {
              ctx.fill('#ff0000');
              for (var i = 0; i < 12; i++) { var p = orbit(i, t); ctx.circle(p.x, p.y, 3); }
              ctx.text('t=' + t.toFixed(1), ctx.width / 2, 20, 16);
            }
            """;
        var cmds = VideoCode.Run(code, 0.5, 200, 200);
        Assert.Equal(12, cmds.Count(c => c.Op == "circle"));
        Assert.Equal(100 + 50 * Math.Cos(0.5), cmds.First(c => c.Op == "circle").Args[0], 9);
        Assert.Equal("t=0.5", cmds.Single(c => c.Op == "text").Text);
        Assert.Equal(100, cmds.Single(c => c.Op == "text").Args[0]);
    }

    [Theory]
    [InlineData("function draw(ctx){ ctx.text(typeof require + typeof importNamespace + typeof fetch + typeof System + typeof process + typeof XMLHttpRequest, 0, 0, 10); }")]
    public void NothingOutsideTheDrawingIsInReach(string code)
    {
        var text = VideoCode.Run(code, 0, 10, 10).Single().Text;
        Assert.Equal(string.Concat(Enumerable.Repeat("undefined", 6)), text);
    }

    [Theory]
    [InlineData("function draw(ctx){ while(true){} }", "instruções")]
    [InlineData("function draw(ctx){ function f(n){ return f(n+1); } f(0); }", "recursão")]
    [InlineData("function draw(ctx){ var a=[]; while(true) a.push(new Array(100000).fill(1)); }", "")]
    [InlineData("function draw(ctx){ for(var i=0;i<100000;i++) ctx.circle(0,0,1); }", "comandos")]
    [InlineData("function draw(ctx){ undefinedThing(); }", "linha 1")]
    public void AProgramThatRunsAwayOrBreaksIsStoppedSayingWhy(string code, string reason)
    {
        var error = Assert.Throws<VideoCodeException>(() => VideoCode.Run(code, 0, 10, 10));
        // an endless loop is stopped by whichever limit comes first: the statements or the frame's time
        if (reason == "instruções") Assert.True(error.Message.Contains("instruções") || error.Message.Contains("ms"), error.Message);
        else Assert.Contains(reason, error.Message);
    }

    [Theory]
    [InlineData("", "vazio")]
    [InlineData("function desenhar(){}", "draw")]
    [InlineData("function draw( {", "JavaScript válido")]
    public void ABadProgramIsRefusedBeforeItRuns(string code, string reason) =>
        Assert.Contains(reason, Assert.Throws<VideoCodeException>(() => VideoCode.Check(code)).Message);

    [Fact]
    public void RandomIsTheSameEveryTimeForTheSameFrameAndElement()
    {
        const string code = "function draw(ctx){ for (var i=0;i<5;i++) ctx.circle(Math.random()*100, noise(i*0.7)*100, 1); }";
        var a = VideoCode.Run(code, 1, 100, 100, seed: "e1").Select(c => c.Args[0]).ToList();
        var b = VideoCode.Run(code, 1, 100, 100, seed: "e1").Select(c => c.Args[0]).ToList();
        var other = VideoCode.Run(code, 1, 100, 100, seed: "e2").Select(c => c.Args[0]).ToList();
        Assert.Equal(a, b);
        Assert.NotEqual(a, other);
    }

    [Fact]
    public void ACodeElementIsCheckedWithTheVideoAndItsFailureIsReviewed()
    {
        var store = new DocumentStore();
        var doc = store.Upsert(new DocumentInput { Path = "Cena.md", Content = "# Cena\n\nTexto.\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create("V", doc.Id) with { Design = new PublicationDesign { Layout = "video" } });
        VideoElement Code(string js) => new() { Id = "prog", Kind = "code", Text = js, X = 0.5, Y = 0.5, Width = 1, Height = 1 };
        var bad = project with { Video = project.Video!.WithScene(project.Video.Scenes[0] with { Duration = 3, Elements = [Code("function draw( {")] }) };
        Assert.Contains("código de prog", Assert.Throws<InvalidDataException>(() => PublicationProjectCodec.Validate(bad)).Message);

        // valid code that fails only after 1 s: the review says where and why
        var late = project with { Video = project.Video.WithScene(project.Video.Scenes[0] with { Duration = 3, Elements = [Code("function draw(ctx, t){ if (t > 1) missing(); ctx.circle(1,1,1); }")] }) };
        PublicationProjectCodec.Validate(late);
        var timeline = VideoTimeline.Build(late, catalog);
        var issue = Assert.Single(VideoInspection.Run(timeline), i => i.ElementId == "prog");
        Assert.Equal(ReviewSeverity.Error, issue.Severity);
        Assert.Contains("missing", issue.Message);
        // every instant is a new frame of a program (nothing cached as still)
        Assert.NotEqual(timeline.Evaluate(0.2).Layers[0].StateKey, timeline.Evaluate(0.3).Layers[0].StateKey);
    }

    [Fact]
    public void ArcsBlendModesAndDashesAreRecordedAndABadModeSaysWhich()
    {
        var cmds = VideoCode.Run("function draw(ctx){ ctx.blend('add'); ctx.stroke('#fff', 4); ctx.dash(8, 4, 2); ctx.arc(50, 50, 20, -90, 180); }", 0, 100, 100);
        Assert.Equal(["blend", "stroke", "dash", "arc"], cmds.Select(c => c.Op));
        Assert.Equal("add", cmds[0].Text);
        Assert.Equal([50.0, 50, 20, -90, 180], cmds[3].Args);
        Assert.Contains("multiply", Assert.Throws<VideoCodeException>(() => VideoCode.Run("function draw(ctx){ ctx.blend('soma'); }", 0, 10, 10)).Message);
    }

    [Fact]
    public void ASimulationGivesTheSameStateAtATimeInOrderOrOutOfOrder()
    {
        const string code = """
            function draw(ctx, t) {
              var s = simulate('queda', function () { return { y: 0, v: 0 }; },
                function (s, dt) { s.v += 9.8 * dt; s.y += s.v * dt; if (s.y > 100) { s.y = 100; s.v = -s.v * 0.8; } }, t, 1 / 120);
              ctx.circle(50, s.y, 5);
            }
            """;
        double Y(double t) => VideoCode.Run(code, t, 100, 100).Single().Args[1];
        var inOrder = new[] { 0.5, 1.0, 2.0, 3.0 }.Select(Y).ToList();
        var jumped = new[] { 3.0, 0.5, 2.0, 1.0 }.Select(t => (t, Y(t))).OrderBy(x => x.t).Select(x => x.Item2).ToList();
        Assert.Equal(inOrder, jumped);
        Assert.Equal(0.5 * 9.8 * 0.25, inOrder[0], 0);   // free fall at 0.5 s, about 1.2 units
        Assert.True(inOrder.All(y => y <= 100));           // it bounced off the floor
    }

    [Fact]
    public void AProgramMeasuresTextToLayItOut()
    {
        var before = VideoCode.Measure;
        VideoCode.Measure = (text, size, bold) => text.Length * size * (bold ? 1 : 0.5);
        try
        {
            var cmds = VideoCode.Run("function draw(ctx){ var w = ctx.measure('abcd', 10); ctx.rect(0, 0, w, 2); ctx.rect(0, 0, ctx.measure('ab', 10, true), 2); }", 0, 100, 100);
            Assert.Equal(20, cmds[0].Args[2]);
            Assert.Equal(20, cmds[1].Args[2]);
        }
        finally { VideoCode.Measure = before; }
    }

    [Fact]
    public void AProgramKnowsTheScenesDuration()
    {
        var cmds = VideoCode.Run("function draw(ctx, t, info){ ctx.rect(0, 0, 100 * t / info.duration, 1); }", 2, 100, 100, duration: 8);
        Assert.Equal(25, cmds.Single().Args[2], 9);
    }

    [Fact]
    public void ColourAnd3dHelpersGiveWhatADrawingNeeds()
    {
        var cmds = VideoCode.Run("""
            function draw(ctx){
              ctx.fill(hsl(0, 1, 0.5)); ctx.fill(hsl(120, 1, 0.5)); ctx.fill(hsl(240, 1, 0.5, 0.5));
              var p = rotateY({ x: 100, y: 0, z: 0 }, 90); ctx.circle(p.x, p.z, 1);
              var q = project(100, 50, 600, 600); ctx.circle(q.x, q.y, q.scale);
            }
            """, 0, 100, 100);
        Assert.Equal(["#ff0000", "#00ff00", "#800000ff"], cmds.Where(c => c.Op == "fill").Select(c => c.Color));
        var circles = cmds.Where(c => c.Op == "circle").ToList();
        Assert.Equal(0, circles[0].Args[0], 6);       // x turned 90° about y goes to depth
        Assert.Equal(-100, circles[0].Args[1], 6);
        Assert.Equal([50, 25, 0.5], circles[1].Args.Select(v => Math.Round(v, 6)));   // twice as far: half the size
    }
}
