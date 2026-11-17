using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Urbe.Core.Study;

namespace Urbe.Client;

/// <summary>#956: the open note as an infographic, Nome.infografico.svg (vector) and .png (to see and embed), side by side.</summary>
public sealed partial class MainView
{
    public bool CanMakeInfographic(string? path) => VaultWritable && path is not null && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public async Task<string?> MakeInfographicAsync(string path)
    {
        if (Editor.Current?.Path == path && Editor.HasChanges && !await Editor.SaveAsync()) return null;
        if (!CanMakeInfographic(path) || !_contents.TryGetValue(path, out var markdown)) return null;
        var title = _store?.Get(path)?.Title ?? LastName(path);
        if (Infographic.Read(markdown, title).Sections.Count == 0)
        {
            await Dialogs.AlertAsync("Gerar infográfico", "A nota precisa de títulos de seção (## Título) para virar infográfico: cada seção vira um cartão.");
            return null;
        }
        var stem = path[..^3] + ".infografico";
        for (int n = 2; _attachments.Contains(stem + ".svg") || _attachments.Contains(stem + ".png"); n++) stem = path[..^3] + ".infografico " + n;
        try
        {
            await ((IAssistantApp)this).CreateBinaryFileAsync(stem + ".svg", System.Text.Encoding.UTF8.GetBytes(Infographic.Svg(markdown, title)));
            await ((IAssistantApp)this).CreateBinaryFileAsync(stem + ".png", Png(markdown, title));
            await SaveHistoryAsync();
            await Editor.Pinned.OpenFileAsync(stem + ".png");
            _notice.Show("Infográfico pronto: " + Path.GetFileName(stem) + ".svg e .png", TimeSpan.FromSeconds(8), kind: Ui.ToastKind.Success);
            return stem + ".png";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            await Dialogs.AlertAsync("Gerar infográfico", StorageProblems.Explain(ex));
            return null;
        }
    }

    /// <summary>The same shapes as the SVG, drawn by the app's own renderer.</summary>
    public static byte[] Png(string markdown, string title)
    {
        var (width, height, _, shapes) = Infographic.Layout(markdown, title);
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        using (var g = bitmap.CreateDrawingContext())
        {
            g.FillRectangle(Brush.Parse(Infographic.Paper), new Rect(0, 0, width, height));
            foreach (var s in shapes)
            {
                var fill = Brush.Parse(s.Fill);
                if (s.Text is null)
                {
                    g.DrawRectangle(fill, s.Stroke is null ? null : new Pen(Brush.Parse(s.Stroke)), new Rect(s.X, s.Y, s.W, s.H), s.W < 10 ? 3 : 20, s.W < 10 ? 3 : 20);
                    continue;
                }
                var text = new FormattedText(s.Text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, weight: s.Bold ? FontWeight.Bold : FontWeight.Normal), s.Size, fill);
                g.DrawText(text, new Point(s.Centered ? s.X - text.Width / 2 : s.X, s.Y - text.Baseline));
            }
        }
        using var output = new MemoryStream();
        bitmap.Save(output, PngBitmapEncoderOptions.Default);
        return output.ToArray();
    }
}
