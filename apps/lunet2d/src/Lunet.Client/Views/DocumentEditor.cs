using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// The files of one tab (code, or notes) and the open document. The text surface is a plain Avalonia
/// <see cref="TextBox"/> for now; the code view of its own over <c>Lunet.Editor.PieceTable</c> (virtualized lines,
/// IME PT-BR, swipe keys) replaces it in Onda 2 (ADR 0013). Saving is verified: the text is read back after writing.
/// </summary>
public sealed class DocumentEditor : UserControl
{
    private readonly LunetShell _shell;
    private readonly WorkspaceTab _tab;
    private readonly TextBox _text = new()
    {
        AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap,
        FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Code,
        Foreground = LunetTokens.Brush(LunetTokens.Color.Code),
        Background = LunetTokens.Brush(LunetTokens.Color.Background),
        BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(0),
        Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S),
    };
    private string? _saved;

    public DocumentEditor(LunetShell shell, WorkspaceTab tab)
    {
        _shell = shell;
        _tab = tab;
        AutomationProperties.SetName(_text, tab == WorkspaceTab.Code ? "Editor de código" : "Editor de notas");
        if (tab == WorkspaceTab.Notes) { _text.FontFamily = LunetTokens.UiFont; _text.FontSize = LunetTokens.Type.Body; _text.TextWrapping = TextWrapping.Wrap; _text.Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary); }
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
    }

    /// <summary>The project-relative path of the open document, or null.</summary>
    public string? OpenPath { get; private set; }
    public TextBox TextBox => _text;
    public bool IsDirty => OpenPath is not null && (_text.Text ?? "") != _saved;

    public void Load(string? path)
    {
        Save();
        if (path is null) path = ProjectFiles.For(_tab, _shell.Workspace.Files()).FirstOrDefault();
        OpenPath = path;
        _saved = path is null ? null : _shell.Workspace.Read(path);
        _text.Text = _saved ?? "";
        Rebuild();
    }

    /// <summary>Writes the open document when it changed, then reads it back; reports a failed write instead of hiding it.</summary>
    public bool Save()
    {
        if (!IsDirty) return true;
        var text = _text.Text ?? "";
        try
        {
            _shell.Workspace.Write(OpenPath!, text);
            if (_shell.Workspace.Read(OpenPath!) != text) throw new IOException("O arquivo gravado não confere com o texto.");
            _saved = text;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _shell.Report($"Não foi possível salvar {OpenPath}: {ex.Message}. O texto continua aberto aqui.");
            return false;
        }
    }

    private void NewNote()
    {
        var existing = _shell.Workspace.Files().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = 1;
        string path;
        do path = $"{Lunet.Core.ProjectNotes.Folder}/nota-{n++}.md"; while (existing.Contains(path));
        _shell.Workspace.Write(path, "# Nova nota\n\n");
        Load(path);
    }

    private void Rebuild()
    {
        var entries = new List<Control>();
        foreach (var file in ProjectFiles.For(_tab, _shell.Workspace.Files()))
        {
            var selected = file == OpenPath;
            var label = LunetUi.Text(file, LunetTokens.Type.Label, selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextSecondary,
                selected ? FontWeight.SemiBold : FontWeight.Normal);
            label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.VerticalAlignment = VerticalAlignment.Center;
            var b = new Button
            {
                Content = label, MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.S, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"), BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            };
            AutomationProperties.SetName(b, "Abrir " + file);
            b.Click += (_, _) => Load(file);
            entries.Add(b);
        }
        if (_tab == WorkspaceTab.Notes)
            entries.Add(LunetUi.Button("Nova nota", ButtonKind.Ghost, LunetIcons.Plus, NewNote));

        if (OpenPath is null && _tab == WorkspaceTab.Notes)
        {
            Content = LunetUi.EmptyState(LunetIcons.Notes, "Nenhuma nota neste jogo",
                "Ideias, regras e roteiro ficam em Notes/ dentro do jogo, em Markdown, no seu disco.",
                LunetUi.Button("Nova nota", ButtonKind.Primary, LunetIcons.Plus, NewNote));
            return;
        }
        var column = new StackPanel { Spacing = 2, Margin = new Thickness(LunetTokens.Space.Xs) };
        var list = new ScrollViewer { Content = column };
        var listHost = new Border
        {
            Child = list, Width = 220, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 1, 0),
        };
        var root = new DockPanel();
        // On a phone the file list collapses: the text gets the width; files are one tap away in the header.
        if (_shell.State.Layout == ShellLayout.Expanded && !_shell.State.CodeAndGameSideBySide)
        {
            foreach (var e in entries) { e.HorizontalAlignment = HorizontalAlignment.Stretch; column.Children.Add(e); }
            DockPanel.SetDock(listHost, Dock.Left);
            root.Children.Add(listHost);
        }
        else
        {
            var strip = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xxs, Margin = new Thickness(LunetTokens.Space.Xs, LunetTokens.Space.Xxs) },
            };
            var row = (StackPanel)strip.Content!;
            foreach (var e in entries) { e.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(e); }
            var stripHost = new Border { Child = strip, Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 0, 1) };
            DockPanel.SetDock(stripHost, Dock.Top);
            root.Children.Add(stripHost);
        }
        if (_text.Parent is Panel p) p.Children.Remove(_text);
        root.Children.Add(_text);
        Content = root;
    }

    /// <summary>Re-lays out the file list for the current shell layout.</summary>
    public void Relayout() => Rebuild();
}
