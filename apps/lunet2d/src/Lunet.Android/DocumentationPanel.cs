using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Lunet.Docs;
using AndroidColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>Painel de documentação offline: busca, guias, namespaces, tipos e membros (dados vindos dos assets).</summary>
internal sealed class DocumentationPanel
{
    private static DocumentationBrowser? s_browser;

    private readonly Activity _activity;
    private readonly DocumentationBrowser _browser;
    private readonly Dialog _dialog;
    private readonly LinearLayout _content;
    private readonly ScrollView _scroll;
    private readonly TextView _title;
    private readonly EditText _search;
    private readonly int _pad;

    private DocumentationPanel(Activity activity, DocumentationBrowser browser)
    {
        _activity = activity;
        _browser = browser;
        _pad = (int)(12 * activity.Resources!.DisplayMetrics!.Density);

        var root = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(AndroidColor.Argb(255, 24, 26, 32));

        var bar = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
        bar.SetPadding(_pad / 2, _pad / 2, _pad / 2, _pad / 2);
        bar.AddView(MakeButton("←", GoBack));
        bar.AddView(MakeButton("⌂", () => Show("home")));
        _search = new EditText(activity) { Hint = "Buscar na API", TextSize = 14 };
        _search.SetSingleLine(true);
        _search.ImeOptions = global::Android.Views.InputMethods.ImeAction.Search;
        _search.EditorAction += (_, e) =>
        {
            var query = _search.Text?.Trim() ?? "";
            if (query.Length > 0) Show("search:" + query);
            e.Handled = true;
        };
        bar.AddView(_search, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        bar.AddView(MakeButton("✕", () => _dialog!.Dismiss()));
        root.AddView(bar);

        _title = new TextView(activity) { TextSize = 20 };
        _title.SetTypeface(null, TypefaceStyle.Bold);
        _title.SetPadding(_pad, _pad / 2, _pad, _pad / 2);
        root.AddView(_title);

        _scroll = new ScrollView(activity);
        _content = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        _content.SetPadding(_pad, 0, _pad, _pad * 2);
        _scroll.AddView(_content);
        root.AddView(_scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        _dialog = new Dialog(activity, global::Android.Resource.Style.ThemeBlackNoTitleBarFullScreen);
        _dialog.SetContentView(root);
        _dialog.KeyPress += (_, e) =>
        {
            if (e.KeyCode == Keycode.Back && e.Event?.Action == KeyEventActions.Up && _browser.CanGoBack)
            {
                GoBack();
                e.Handled = true;
            }
            else e.Handled = false;
        };
    }

    /// <summary>Abre a documentação em <paramref name="target"/> (padrão: início). Retorna falso se os assets não puderem ser lidos.</summary>
    public static bool Open(Activity activity, string target = "home")
    {
        try
        {
            s_browser ??= Load(activity);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            Toast.MakeText(activity, "Documentação indisponível: " + ex.Message, ToastLength.Long)?.Show();
            return false;
        }
        var panel = new DocumentationPanel(activity, s_browser);
        panel._dialog.Show();
        panel.Show(target);
        return true;
    }

    /// <summary>Alvo de navegação para um identificador de documentação do Roslyn, ou busca pelo nome.</summary>
    public static string? TargetFor(Activity activity, string? documentationId, string name)
    {
        try
        {
            s_browser ??= Load(activity);
            return s_browser.TargetForSymbol(documentationId, name);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static DocumentationBrowser Load(Activity activity)
    {
        var assets = activity.Assets!;
        string ReadAll(string path)
        {
            using var stream = assets.Open(path);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        var api = ApiDocumentation.FromJson(ReadAll("docs/lunet-framework.json"));
        var guides = new Dictionary<string, string>();
        foreach (var file in assets.List("docs/guides") ?? [])
            if (file.EndsWith(".md", StringComparison.Ordinal))
                guides[Path.GetFileNameWithoutExtension(file)] = ReadAll("docs/guides/" + file);
        return new DocumentationBrowser(api, guides);
    }

    private Button MakeButton(string label, Action action)
    {
        var button = new Button(_activity) { Text = label };
        button.Click += (_, _) => action();
        return button;
    }

    private void GoBack()
    {
        var page = _browser.Back();
        if (page is null) _dialog.Dismiss();
        else Render(page);
    }

    private void Show(string target) => Render(_browser.Navigate(target));

    private void Render(DocPage page)
    {
        _title.Text = page.Title;
        _content.RemoveAllViews();
        foreach (var block in page.Blocks) _content.AddView(BuildBlock(block));
        _scroll.ScrollTo(0, 0);
    }

    private View BuildBlock(DocBlock block)
    {
        var view = new TextView(_activity) { TextSize = 14 };
        view.SetTextColor(AndroidColor.Argb(255, 220, 223, 228));
        view.SetPadding(0, _pad / 3, 0, _pad / 3);
        switch (block.Kind)
        {
            case DocBlockKind.Title:
                view.TextSize = 22;
                view.SetTypeface(null, TypefaceStyle.Bold);
                view.Text = block.Text;
                break;
            case DocBlockKind.Heading:
                view.TextSize = 17;
                view.SetTypeface(null, TypefaceStyle.Bold);
                view.SetTextColor(AndroidColor.Argb(255, 130, 180, 255));
                view.SetPadding(0, _pad, 0, _pad / 3);
                view.Text = block.Text;
                break;
            case DocBlockKind.Signature:
            case DocBlockKind.Code:
                view.TextSize = 12;
                view.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
                view.SetBackgroundColor(AndroidColor.Argb(255, 14, 15, 19));
                view.SetPadding(_pad / 2, _pad / 2, _pad / 2, _pad / 2);
                view.Text = block.Text;
                break;
            case DocBlockKind.Note:
                view.TextSize = 12;
                view.SetTextColor(AndroidColor.Argb(255, 150, 155, 165));
                view.Text = block.Text;
                break;
            case DocBlockKind.Bullet:
                view.TextFormatted = Inline("• " + block.Text);
                break;
            case DocBlockKind.Link:
                var text = new SpannableStringBuilder(block.Text);
                text.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, text.Length(), SpanTypes.ExclusiveExclusive);
                if (!string.IsNullOrEmpty(block.Detail)) text.Append("\n" + block.Detail);
                view.TextFormatted = text;
                view.SetTextColor(AndroidColor.Argb(255, 130, 180, 255));
                view.SetPadding(0, _pad / 2, 0, _pad / 2);
                var target = block.Target!;
                view.Click += (_, _) => Show(target);
                break;
            default:
                view.TextFormatted = Inline(block.Text);
                break;
        }
        return view;
    }

    private static SpannableStringBuilder Inline(string text)
    {
        var builder = new SpannableStringBuilder();
        foreach (var run in MarkdownLite.ParseInline(text))
        {
            var start = builder.Length();
            builder.Append(run.Text);
            var end = builder.Length();
            switch (run.Style)
            {
                case InlineStyle.Bold:
                    builder.SetSpan(new StyleSpan(TypefaceStyle.Bold), start, end, SpanTypes.ExclusiveExclusive);
                    break;
                case InlineStyle.Italic:
                    builder.SetSpan(new StyleSpan(TypefaceStyle.Italic), start, end, SpanTypes.ExclusiveExclusive);
                    break;
                case InlineStyle.Code:
                    builder.SetSpan(new TypefaceSpan("monospace"), start, end, SpanTypes.ExclusiveExclusive);
                    builder.SetSpan(new ForegroundColorSpan(AndroidColor.Argb(255, 240, 200, 120)), start, end, SpanTypes.ExclusiveExclusive);
                    break;
            }
        }
        return builder;
    }
}
