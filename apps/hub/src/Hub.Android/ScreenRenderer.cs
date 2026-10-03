using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Hub.Core;

namespace HubApp;

/// <summary>Desenha um <see cref="HubScreen"/>. Sem decisão: o que mostrar e o que avisar já veio decidido e testado do Hub.Core.</summary>
sealed class ScreenRenderer(Context context, LinearLayout container, Action<string>? openChanges = null)
{
    public static int Dp(Context context, float dp) =>
        (int)((dp * (context.Resources?.DisplayMetrics?.Density ?? 1f)) + 0.5f);

    public void Render(HubScreen screen)
    {
        container.RemoveAllViews();
        if (screen.Banner is { } banner) container.AddView(Banner(banner));
        foreach (var section in screen.Sections)
        {
            container.AddView(SectionTitle(section.Title));
            foreach (var line in section.Lines) container.AddView(Line(line));
        }
    }

    TextView Banner(string text)
    {
        var view = new TextView(context) { Text = text, TextSize = 14 };
        view.SetBackgroundColor(Color.ParseColor("#FFF3CD"));
        view.SetTextColor(Color.ParseColor("#664D03"));
        view.SetPadding(Dp(context, 16), Dp(context, 10), Dp(context, 16), Dp(context, 10));
        return view;
    }

    TextView SectionTitle(string title)
    {
        var view = new TextView(context) { Text = title, TextSize = 18 };
        view.SetTypeface(null, TypefaceStyle.Bold);
        view.SetPadding(Dp(context, 16), Dp(context, 20), Dp(context, 16), Dp(context, 6));
        return view;
    }

    LinearLayout Line(ScreenLine line)
    {
        var box = new LinearLayout(context) { Orientation = Orientation.Vertical };
        box.SetPadding(Dp(context, 16), Dp(context, 4), Dp(context, 16), Dp(context, 4));
        box.AddView(new TextView(context) { Text = line.Text, TextSize = 16 });
        if (line.Detail is { } detail)
            box.AddView(new TextView(context) { Text = detail, TextSize = 13, Alpha = 0.7f });
        if (line.ProductId is { } productId && openChanges is not null)
        {
            var changes = new Button(context) { Text = "Ver mudanças" };
            changes.Click += (_, _) => openChanges(productId);
            box.AddView(changes);
        }
        return box;
    }
}
