using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Client.Ui;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-69 phase 1 (UX.md): the design tokens, the shared components (button, icon button, toast, bottom sheet,
/// transitions, haptics) and the performance budgets of the touch → frame path.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static Point Centre(Control c, Window window) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;

    [Fact]
    public void Ux_TokensAreOneSystemOverThe184Palette()
    {
        foreach (var s in new[] { UrbeTokens.Space.Xxs, UrbeTokens.Space.Xs, UrbeTokens.Space.S, UrbeTokens.Space.M, UrbeTokens.Space.L, UrbeTokens.Space.Xl })
            Assert.Equal(0, s % 4);
        Assert.Equal(48, UrbeTokens.MinTouch);
        Assert.InRange(UrbeTokens.Motion.Press.TotalMilliseconds, 1, 99);
        Assert.InRange(UrbeTokens.Motion.Fast.TotalMilliseconds, 150, 250);
        Assert.InRange(UrbeTokens.Motion.Normal.TotalMilliseconds, 150, 250);
        Assert.InRange(UrbeTokens.Motion.Slow.TotalMilliseconds, 250, 300);
        // the semantic names are the 1.8.4 values, not new colours
        Assert.Equal(UrbeTheme.Accent, UrbeTokens.Color.Primary);
        Assert.Equal(UrbeTheme.Bg, UrbeTokens.Color.Background);
        Assert.Equal(UrbeTheme.Danger, UrbeTokens.Color.Danger);
        Assert.True(UrbeTokens.Type.Caption.Size < UrbeTokens.Type.Body.Size && UrbeTokens.Type.Body.Size < UrbeTokens.Type.Title.Size && UrbeTokens.Type.Title.Size < UrbeTokens.Type.Headline.Size);
        // the springy curve settles at its end
        Assert.Equal(1, UrbeTokens.Motion.Spring.Ease(1), 3);
        Assert.Equal(0, UrbeTokens.Motion.EaseOut.Ease(0), 3);
    }

    [Fact]
    public async Task Ux_ButtonsReactInTheSameInputEventWithAHapticTick() => Assert.True(await Session.Dispatch(() =>
    {
        var haptics = new List<HapticKind>();
        void Record(HapticKind k) => haptics.Add(k);
        PlatformHaptics.Requested += Record;
        try
        {
            var primary = new UrbeButton("Criar nota", ButtonKind.Primary);
            var icon = new UrbeIconButton(UrbeTheme.Icons.Close, "Fechar");
            var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Spacing = 8, Margin = new Thickness(16), Children = { primary, icon } } };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.True(primary.Bounds.Height >= UrbeTokens.MinTouch && icon.Bounds.Width >= UrbeTokens.MinTouch && icon.Bounds.Height >= UrbeTokens.MinTouch);
            Assert.Equal("Criar nota", AutomationProperties.GetName(primary));
            Assert.Equal("Fechar", AutomationProperties.GetName(icon));
            int clicks = 0;
            primary.Click += (_, _) => clicks++;
            var p = Centre(primary, window);
            // the press shows within the input event itself; timing only it keeps the other tests' jobs on the shared
            // dispatcher out of the measure (as the typing budget does)
            var watch = Stopwatch.StartNew();
            window.MouseDown(p, MouseButton.Left);
            watch.Stop();
            Assert.True(UrbeMotion.ShowsPressed(primary));
            // the budget is measured where nothing else shares the dispatcher (URBE_PERF=1, like StartupPerfTests): in the
            // full suite each headless input also renders every other test's open window (CI: 800 ms for a press that
            // takes a few ms alone), so there the press itself is what is checked
            PerfLog($"ux.press {watch.ElapsedMilliseconds} ms");
            if (Environment.GetEnvironmentVariable("URBE_PERF") == "1") Assert.True(watch.ElapsedMilliseconds < 100, watch.ElapsedMilliseconds + " ms");
            Dispatcher.UIThread.RunJobs();
            Assert.True(UrbeMotion.ShowsPressed(primary));
            Assert.Contains(HapticKind.Tick, haptics);
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(UrbeMotion.ShowsPressed(primary));
            Assert.Equal(1, clicks);
            window.Close();
            return true;
        }
        finally { PlatformHaptics.Requested -= Record; }
    }, CancellationToken.None));

    [Fact]
    public async Task Ux_ExplorerAndEditorHeaderButtonsShowTheirPressInOneDispatcherPass() => Assert.True(await Session.Dispatch(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        await view.LoadAsync(TutorialNotes.Load());
        await Settle(window, view);
        static bool Shown(Control c) => c.GetSelfAndLogicalAncestors().OfType<Control>().All(x => x.IsVisible);
        Button Named(Control root, string name) => root.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => Shown(b) && (AutomationProperties.GetName(b) ?? ToolTip.GetTip(b) as string) == name)
            ?? throw new InvalidOperationException(name + " not in: " + string.Join(", ", root.GetLogicalDescendants().OfType<Button>().Where(Shown).Select(b => AutomationProperties.GetName(b) ?? ToolTip.GetTip(b) as string ?? "?")));

        foreach (var name in new[] { "Nova nota", "Mais opções", "Configurações", "Assistente" })
        {
            var b = Named(view, name);
            var p = Centre(b, window);
            window.MouseDown(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(UrbeMotion.ShowsPressed(b), name);
            // released outside: no command runs, the button springs back
            window.MouseMove(new Point(2, 430));
            window.MouseUp(new Point(2, 430), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(UrbeMotion.ShowsPressed(b), name);
        }
        Assert.False(view.Dialogs.IsOpen);

        view.OpenNoteByPath(TutorialNotes.Load()[0].Path);
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.Editor.IsVisible);
        Assert.Equal(1, view.Editor.Opacity);   // reduced motion in tests: the overlay is there at once
        foreach (var name in new[] { "Fonte", "Mais ações", "Voltar" })
        {
            var b = Named(view.Editor, name);
            var p = Centre(b, window);
            window.MouseDown(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(UrbeMotion.ShowsPressed(b), name);
            window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(UrbeMotion.ShowsPressed(b), name);
            if (name == "Fonte") Assert.Equal(EditorMode.Fonte, view.Editor.Mode);   // the tap's effect is synchronous too
            if (name == "Mais ações")
            {
                Assert.True(view.ActionSheet.IsOpen);   // UC-69: the note's actions rise in a bottom sheet
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                for (int i = 0; i < 20 && view.ActionSheet.IsVisible; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            }
        }
        for (int i = 0; i < 20 && view.Editor.IsVisible; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
        Assert.False(view.Editor.IsVisible);   // Voltar closed the editor
        Assert.True(view.Explorer.IsVisible);  // and the Explorer is under it
        window.Close();
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Ux_TransitionsKeepIsVisibleMeaning() => Assert.True(await Session.Dispatch(async () =>
    {
        var overlay = new Border { Width = 200, Height = 100, IsVisible = false };
        var window = new Window { Width = 300, Height = 200, Content = new Grid { Children = { overlay } } };
        window.Show();
        overlay.IsVisible = true;
        UrbeMotion.Enter(overlay, 0, 12, animate: true);
        Assert.True(overlay.IsVisible);      // shown at once; the fade only paints the way in
        Assert.NotNull(overlay.Transitions);
        for (int i = 0; i < 60 && overlay.Opacity < 1; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.Equal(1, overlay.Opacity);
        var hide = UrbeMotion.HideAsync(overlay, animate: true);
        Assert.True(overlay.IsVisible);      // leaving: still there while it fades
        for (int i = 0; i < 50 && !hide.IsCompleted; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        await hide;
        Assert.False(overlay.IsVisible);
        Assert.Equal(1, overlay.Opacity);    // ready for the next show
        Assert.Null(overlay.RenderTransform);
        // a show during the leave wins
        overlay.IsVisible = true;
        var again = UrbeMotion.HideAsync(overlay, animate: true);
        UrbeMotion.Enter(overlay, animate: true);
        await again;
        Assert.True(overlay.IsVisible);
        window.Close();
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Ux_ToastInformsWithoutTakingTheTouchAndGoesAway() => Assert.True(await Session.Dispatch(async () =>
    {
        var haptics = new List<HapticKind>();
        void Record(HapticKind k) => haptics.Add(k);
        PlatformHaptics.Requested += Record;
        try
        {
            var toast = new UrbeToast();
            var window = new Window { Width = 412, Height = 300, Content = new Grid { Children = { toast } } };
            window.Show();
            toast.Show("Nota criada.", TimeSpan.FromMilliseconds(80), kind: ToastKind.Success);
            Assert.Equal("Nota criada.", toast.Text);
            Assert.False(toast.IsHitTestVisible);
            Assert.Equal("Nota criada.", AutomationProperties.GetName(toast));
            Assert.Contains(HapticKind.Confirm, haptics);
            for (int i = 0; i < 40 && toast.IsShown; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Null(toast.Text);
            // with an action it is touchable, and the action closes it
            bool undone = false;
            toast.Show("Seção removida.", action: "Desfazer", onAction: () => undone = true);
            Assert.True(toast.IsHitTestVisible);
            var undo = toast.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Desfazer");
            undo.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.True(undone);
            Assert.False(toast.IsShown);
            window.Close();
            return true;
        }
        finally { PlatformHaptics.Requested -= Record; }
    }, CancellationToken.None));

    [Fact]
    public async Task Ux_BottomSheetSwipesDownToDismissAndSpringsBackFromAShortDrag() => Assert.True(await Session.Dispatch(async () =>
    {
        var sheet = new BottomSheet();
        var window = new Window { Width = 412, Height = 860, Content = new Grid { Children = { sheet } } };
        window.Show();
        int closed = 0;
        sheet.Closed += () => closed++;
        sheet.Open(new StackPanel { Height = 300, Children = { new TextBlock { Text = "Conteúdo" } } });
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Assert.True(sheet.IsOpen && sheet.IsVisible);
        Assert.Equal("Fechar", AutomationProperties.GetName(sheet.Handle));
        Assert.True(sheet.Handle.Bounds.Height >= UrbeTokens.MinTouch);
        var start = Centre(sheet.Handle, window);
        // a short drag: the sheet follows and comes back
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(0, 40));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(40, sheet.DragOffset, 0);
        window.MouseUp(start + new Point(0, 40), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(sheet.IsOpen);
        Assert.Equal(0, sheet.DragOffset);
        // past a third of its height: closes
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(0, 120));
        window.MouseMove(start + new Point(0, 260));
        window.MouseUp(start + new Point(0, 260), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.False(sheet.IsOpen);
        for (int i = 0; i < 20 && closed == 0; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
        Assert.Equal(1, closed);
        Assert.False(sheet.IsVisible);
        // the handle is a button too (an alternative to the gesture, P8)
        sheet.Open(new TextBlock { Text = "De novo" });
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var h = Centre(sheet.Handle, window);
        window.MouseDown(h, MouseButton.Left);
        window.MouseUp(h, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.False(sheet.IsOpen);
        window.Close();
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Ux_AnAlertThatOnlyInformsIsAToast() => Assert.True(await Session.Dispatch(async () =>
    {
        var (view, window, _, dir) = await OpenCreateVault();
        try
        {
            await view.CleanOrphansAsync();   // returns at once: nothing waits for an Ok
            Assert.False(view.Dialogs.IsOpen);
            Assert.StartsWith("Nada para limpar", view.NoticeText);
            window.Close();
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Ux_ComponentsLookRight() => Assert.True(await Session.Dispatch(async () =>
    {
        var toast = new UrbeToast(top: 24);
        var sheet = new BottomSheet();
        var kinds = new StackPanel { Spacing = UrbeTokens.Space.Xs };
        foreach (var (label, kind) in new[] { ("Criar nota", ButtonKind.Primary), ("Cancelar", ButtonKind.Secondary), ("Mais tarde", ButtonKind.Ghost), ("Excluir", ButtonKind.Danger) })
            kinds.Children.Add(new UrbeButton(label, kind, label == "Criar nota" ? UrbeTheme.Icons.Plus : null) { HorizontalAlignment = HorizontalAlignment.Stretch });
        kinds.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = UrbeTokens.Space.Xs,
            Children = { new UrbeIconButton(UrbeTheme.Icons.Back, "Voltar"), new UrbeIconButton(UrbeTheme.Icons.SearchUi, "Buscar", round: true), new UrbeIconButton(UrbeTheme.Icons.MoreUi, "Mais ações") }
        });
        var page = new Grid { Background = UrbeTheme.Brush(UrbeTheme.Bg), Children = { new Border { Padding = new Thickness(UrbeTokens.Space.M, 80, UrbeTokens.Space.M, 0), Child = kinds }, toast, sheet } };
        var window = new Window { Width = 412, Height = 860, Content = page };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        toast.Show("Nota criada e salva no vault.", kind: ToastKind.Success);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Snapshot(window, "native-ux-components.png");
        var body = new StackPanel { Spacing = UrbeTokens.Space.S };
        body.Children.Add(UrbeTokens.Type.Title.Apply(new TextBlock { Text = "Nova nota", Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextPrimary) }));
        body.Children.Add(UrbeTokens.Type.Callout.Apply(new TextBlock { Text = "Escolha onde a casa vai morar. Dá para mudar depois, arrastando no mapa.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextSecondary) }));
        body.Children.Add(new UrbeButton("Criar aqui", ButtonKind.Primary) { HorizontalAlignment = HorizontalAlignment.Stretch });
        body.Children.Add(new UrbeButton("Escolher bairro", ButtonKind.Secondary) { HorizontalAlignment = HorizontalAlignment.Stretch });
        sheet.Open(body);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Snapshot(window, "native-ux-bottom-sheet.png");
        await Task.CompletedTask;
        window.Close();
        return true;
    }, CancellationToken.None));

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Ux_TypingInFonteOfA5KbNoteStaysWithinTheFrameBudget() => Assert.True(await Session.Dispatch(async () =>
    {
        var paragraph = "Uma linha da nota com [[Ligação]], **negrito** e um #Tag: para o editor ter trabalho de verdade.\n\n";
        var text = "# Nota grande\n\n" + string.Concat(Enumerable.Repeat(paragraph, 5 * 1024 / paragraph.Length + 1));
        Assert.True(text.Length >= 5 * 1024);
        var view = new MainView();
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        await view.LoadAsync(TutorialNotes.Load());
        await Settle(window, view);
        view.Editor.Open(new CityNote("Tutorial/Grande.md", text));
        view.Editor.SetMode(EditorMode.Fonte);
        Dispatcher.UIThread.RunJobs();
        view.Editor.Editor.Focus();
        view.Editor.Editor.CaretIndex = text.Length / 2;
        window.KeyTextInput("a");   // warm up
        Dispatcher.UIThread.RunJobs();
        // the input is handled synchronously (text, layout, the editor's and the host's handlers): timing only it keeps
        // the other tests' jobs on the shared dispatcher out of the measure
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) window.KeyTextInput("a");
        watch.Stop();
        Dispatcher.UIThread.RunJobs();
        double each = watch.Elapsed.TotalMilliseconds / 100;
        Assert.Equal(text.Length + 101, view.Editor.Editor.Text!.Length);
        PerfLog($"ux.typing {each:0.00} ms per keystroke");
        // same as the press budget: only meaningful alone (URBE_PERF=1); in the full suite each keystroke also renders
        // the other tests' open windows (CI: ~700 ms per key against ~14 ms alone)
        if (Environment.GetEnvironmentVariable("URBE_PERF") == "1") Assert.True(each < 25, each.ToString("0.00") + " ms per keystroke");
        await view.Editor.BackAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.Editor.IsVisible);
        window.Close();
        return true;
    }, CancellationToken.None));
}
