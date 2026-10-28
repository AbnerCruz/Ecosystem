using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// <c>![[destino]]</c> in the Visual editor (24/10/2026): the note, image or file it names shown in place, in a card that
/// takes the size of what it shows and scrolls only when that is extreme — the point is to see the content at a glance.
/// A tap on the card's header opens the target; a tap on its body edits the line, like any block.
/// </summary>
public sealed partial class MainView
{
    private const double EmbedMaxHeight = 340;
    private const long EmbedImageMax = 16L * 1024 * 1024, EmbedTextMax = 256L * 1024;
    private const int EmbedTextLines = 14;
    private const long EmbedPageMax = 32L * 1024 * 1024;
    private const double EmbedPageHeight = 420;

    /// <summary>The card of <c>![[</c><paramref name="target"/><c>]]</c>: never null, never throws (a missing target says so).</summary>
    internal Control BuildEmbed(string target)
    {
        try
        {
            var name = target;
            var cut = name.IndexOfAny(['|', '#']);
            if (cut >= 0) name = name[..cut];
            name = name.Trim();
            if (name.Length == 0) return Missing(target);
            if (FindEmbeddedGraph(name) is { } graph) return GraphCard(graph);
            var file = FindEmbeddedFile(name);
            if (file is not null && !name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return FileCard(file);
            if (FindEmbeddedNote(name) is { } note) return NoteCard(note);
            return file is not null ? FileCard(file) : Missing(target);
        }
        catch (Exception ex) { return Missing(target + " (" + ex.Message + ")"); }
    }

    private string? FindEmbeddedNote(string name)
    {
        if (_contents.ContainsKey(name)) return name;
        if (_contents.ContainsKey(name + ".md")) return name + ".md";
        var byName = _contents.Keys.Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (byName.Count == 1) return byName[0];
        var byAlias = _contents.Where(c => c.Key.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && NoteProperties.AliasesOf(c.Value).Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase))).Select(c => c.Key).ToList();
        return byAlias.Count == 1 ? byAlias[0] : null;
    }

    private string? FindEmbeddedFile(string name)
    {
        // the vault's other files; text files that are not notes (.html, .txt, .csv…) live with the notes' texts
        var files = _attachments.Concat(_contents.Keys.Where(p => !p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))).ToList();
        var exact = files.FirstOrDefault(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;
        var byName = files.Where(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase)).ToList();
        return byName.Count == 1 ? byName[0] : null;
    }


    /// <summary>Resolve ![[Dinastia]] and ![[Pasta/Dinastia.grafo.json]] without ambiguous basename guesses.</summary>
    private string? FindEmbeddedGraph(string name)
    {
        var paths = _attachments.Where(GraphDocument.IsGraphPath).ToArray();
        if (paths.FirstOrDefault(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)) is { } exact)
            return exact;
        if (paths.FirstOrDefault(p => string.Equals(p, name + GraphDocument.FileSuffix, StringComparison.OrdinalIgnoreCase)) is { } suffixed)
            return suffixed;
        var matches = paths.Where(p => string.Equals(Path.GetFileName(p)[..^GraphDocument.FileSuffix.Length],
            name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private Control GraphCard(string path)
    {
        var title = Path.GetFileName(path)[..^GraphDocument.FileSuffix.Length];
        var holder = new Border { MinHeight = 230, Child = Message("Carregando grafo…") };
        var card = Frame(UrbeTheme.Icons.Layers, title, "Grafo interativo", holder, () => _ = OpenGraphAsync(path));
        _ = FillGraphCardAsync(holder, path);
        return card;
    }

    private async Task FillGraphCardAsync(Border holder, string path)
    {
        try
        {
            if (_vault is null) { holder.Child = Message("Abra o vault para visualizar o grafo."); return; }
            var file = await _vault.ReadFileAsync(path, long.MaxValue);
            if (file?.Bytes is not { } bytes) throw new InvalidDataException("Grafo não encontrado ou ilegível.");
            var document = GraphDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
            var view = new GraphCanvasView { Height = 300 };
            view.Show(document, readOnly: true);
            holder.Child = view;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            holder.Child = Message("Não foi possível carregar o grafo: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ the card

    private static Border Frame(string icon, string title, string? meta, Control body, Action open)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Background = Brushes.Transparent, MinHeight = 38, Margin = new Thickness(12, 0, 8, 0) };
        var glyph = UrbeTheme.Icon(icon, 17, UrbeTheme.Accent, 1.7);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        glyph.Margin = new Thickness(0, 0, 9, 0);
        var heading = new TextBlock
        {
            Text = title, FontSize = 14, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(heading, 1);
        var info = new TextBlock
        {
            Text = meta ?? "", FontSize = 11.5, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0), IsVisible = !string.IsNullOrEmpty(meta)
        };
        Grid.SetColumn(info, 2);
        var go = new TextBlock { Text = "›", FontSize = 20, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, -2, 4, 0) };
        Grid.SetColumn(go, 3);
        header.Children.Add(glyph);
        header.Children.Add(heading);
        header.Children.Add(info);
        header.Children.Add(go);
        header.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        header.Tapped += (_, e) => { e.Handled = true; if (TouchIntent.IsIntended(header)) open(); };
        Avalonia.Automation.AutomationProperties.SetName(header, "Abrir " + title);
        var divider = new Border { Height = 1, Background = UrbeTheme.Brush(UrbeTheme.Line2, .7) };
        var card = new Border
        {
            CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), ClipToBounds = true,
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), Background = UrbeTheme.Brush(UrbeTheme.Accent, .045),
            Child = new StackPanel { Children = { header, divider, body } }
        };
        card.Classes.Add("urbe-embed");
        return card;
    }

    private static Control Message(string text) => new TextBlock
    {
        Text = text, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14, 12)
    };

    private Control Missing(string target) => new Border
    {
        CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
        Background = UrbeTheme.Brush(UrbeTheme.Warn, .06),
        Child = Message("Não encontrei “" + target + "” no vault.")
    };

    /// <summary>A body that grows with its content up to <see cref="EmbedMaxHeight"/>, then scrolls (never sideways).</summary>
    private static ScrollViewer Scrolling(Control content) => new()
    {
        Content = content, MaxHeight = EmbedMaxHeight,
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
    };

    // ------------------------------------------------------------------ a note

    private Control NoteCard(string path)
    {
        var content = _contents.GetValueOrDefault(path, "");
        var body = MarkdownEngine.SplitFrontmatter(content).Body;
        var title = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path);
        var words = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var meta = words == 1 ? "1 palavra" : words + " palavras";
        Control shown;
        if (body.Trim().Length == 0) shown = Message("Nota vazia.");
        else
        {
            var reader = new MarkdownReader { Size = UrbeTheme.EditorSize * .94, LineFactor = 1.55, WikiLinkTapped = name => _ = Editor.FollowLinkAsync(name) };
            var text = body.Length > 40_000 ? body[..40_000] + "\n\n…" : body;
            var view = reader.BuildMarkdown(NoteMarks.StripComments(text));
            // this text is not the block's own: the caret mapping of a tap must not look into it
            foreach (var tb in view.GetLogicalDescendants().OfType<TextBlock>()) tb.Classes.Remove(MarkdownReader.TextClass);
            shown = new Border { Padding = new Thickness(14, 4, 14, 8), Child = view };
        }
        return Frame(UrbeTheme.Icons.Book, title, meta, Scrolling(shown), () => _ = Editor.FollowLinkAsync(path));
    }

    // ------------------------------------------------------------------ an image or another file

    private Control FileCard(string path)
    {
        var name = Path.GetFileName(path);
        var kind = VaultFileKinds.Classify(path);
        var holder = new Border { MinHeight = 56, Child = Message("Carregando…") };
        var card = Frame(kind == VaultFileKinds.Image ? UrbeTheme.Icons.Image : UrbeTheme.Icons.FileOther, name, VaultFileKinds.Label(kind), holder, () => _ = ShowFileAsync(path));
        _ = FillFileCardAsync(holder, card, path, kind);
        return card;
    }

    private async Task FillFileCardAsync(Border holder, Border card, string path, string kind)
    {
        try
        {
            if (_vault is null) { holder.Child = Message("Abra a pasta do vault para ver o arquivo."); return; }
            bool image = kind == VaultFileKinds.Image && !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
            // audio and video are played from the vault by the platform (only their size is read); a page or a PDF is read
            // whole and drawn by the platform's viewer, the same one of the file preview
            bool played = kind is VaultFileKinds.Audio or VaultFileKinds.Video;
            bool viewed = played || kind is VaultFileKinds.Pdf or VaultFileKinds.Html;
            bool media = viewed || kind == VaultFileKinds.Zip;
            var read = await _vault.ReadFileAsync(path, image ? EmbedImageMax : played ? 0 : viewed ? EmbedPageMax : EmbedTextMax);
            if (read is null) { holder.Child = Message("Arquivo não encontrado na pasta do vault."); return; }
            SetMeta(card, VaultFileKinds.Label(kind) + " · " + VaultFileKinds.FormatBytes(read.Size));
            if (image && read.Bytes is { Length: > 0 } bytes)
            {
                using var stream = new MemoryStream(bytes);
                var bitmap = Bitmap.DecodeToWidth(stream, 1280);
                holder.Child = new Image
                {
                    Source = bitmap, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                    MaxHeight = EmbedMaxHeight + 80, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(8)
                };
                return;
            }
            if (viewed)
            {
                var name = Path.GetFileName(path);
                Control? viewer = null;
                try { viewer = await ContentViewers.CreateAsync(new ContentRequest(_vault, path, name, kind, read.Bytes) { AtlasOpenNote = TryOpenAtlasNoteAsync }); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException) { viewer = null; }
                if (viewer is not null)
                {
                    // a web view or a player has no size of its own: a page and a PDF get a window, media what it needs
                    viewer.Height = kind switch { VaultFileKinds.Audio => 96, VaultFileKinds.Video => 230, _ => EmbedPageHeight };
                    // Android NativeControlHost views render above Avalonia overlays. Suspend an embedded
                    // HTML/media viewer while the file preview sheet is on top of the editor.
                    SuspendEmbedUnderFilePreview(viewer);
                    holder.Child = viewer;
                    return;
                }
                holder.Child = Message("Este aparelho não mostra " + VaultFileKinds.Label(kind).ToLowerInvariant() + " aqui. Toque no título para abrir.");
                return;
            }
            if (!media && !image && read.Bytes is { } raw && LooksLikeText(raw))
            {
                var text = System.Text.Encoding.UTF8.GetString(raw);
                var lines = text.Replace("\r\n", "\n").Split('\n');
                var shown = string.Join("\n", lines.Take(EmbedTextLines));
                if (lines.Length > EmbedTextLines) shown += "\n…";
                holder.Child = Scrolling(new TextBlock
                {
                    Text = shown, FontFamily = UrbeTheme.MonoFont, FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Text2), Margin = new Thickness(14, 10)
                });
                return;
            }
            holder.Child = Message(media ? "Toque no título para abrir." : "Sem prévia para este tipo de arquivo. Toque no título para abrir.");
        }
        catch (Exception ex) { holder.Child = Message("Não foi possível mostrar o arquivo: " + ex.Message); }
    }

    private static void SetMeta(Border card, string meta)
    {
        if (card.Child is StackPanel { Children: [Grid header, ..] } && header.Children.Count > 2 && header.Children[2] is TextBlock info)
        {
            info.Text = meta;
            info.IsVisible = true;
        }
    }

    /// <summary>
    /// A WebView inside an embedded file otherwise draws *over* the Fonte sheet on Android,
    /// leaving the source partly hidden by the very page it is meant to inspect.
    /// Subscribe only while the card belongs to the visual tree to avoid retaining old notes.
    /// </summary>
    private void SuspendEmbedUnderFilePreview(Control viewer)
    {
        void Update(object? _, AvaloniaPropertyChangedEventArgs changed)
        {
            if (changed.Property == IsVisibleProperty)
                viewer.IsVisible = !FilePreview.IsVisible;
        }

        viewer.AttachedToVisualTree += (_, _) =>
        {
            FilePreview.PropertyChanged += Update;
            viewer.IsVisible = !FilePreview.IsVisible;
        };
        viewer.DetachedFromVisualTree += (_, _) => FilePreview.PropertyChanged -= Update;
        viewer.IsVisible = !FilePreview.IsVisible;
    }

    private static bool LooksLikeText(byte[] bytes)
    {
        var n = Math.Min(bytes.Length, 4096);
        for (int i = 0; i < n; i++) if (bytes[i] == 0) return false;
        return true;
    }
}
