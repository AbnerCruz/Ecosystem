using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// UC-58 (16/10/2026, without the world since ADD-0028): the first opening, in three steps — the notes' folder, how the
/// app looks and the Assistente's model (or later). Each step is skippable and writes nothing before its own choice; it
/// can be done again from Configurações → Configuração inicial.
/// </summary>
public sealed class FirstRunView : Grid
{
    private readonly StackPanel _body = new() { Spacing = 14, MaxWidth = 520, Margin = new Thickness(24, 40), VerticalAlignment = VerticalAlignment.Center };
    private int _step;

    /// <summary>Escolher pasta: the host opens the folder picker; true when a vault was opened.</summary>
    public Func<Task<bool>>? ChooseFolder { get; set; }
    /// <summary>A theme chosen (its id); the host saves it in Personalização/tema.json.</summary>
    public Func<string, Task>? ChooseTheme { get; set; }
    /// <summary>Done: true when the person asked to connect a model now.</summary>
    public event Action<bool>? Finished;

    /// <summary>The step shown now (0 pasta, 1 aparência, 2 Assistente) — tests.</summary>
    public int Step => _step;

    public FirstRunView()
    {
        IsVisible = false;
        ZIndex = 5;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        Children.Add(new ScrollViewer { Content = _body });
    }

    public void Start(int step = 0)
    {
        IsVisible = true;
        Show(step);
    }

    private void Show(int step)
    {
        _step = step;
        _body.Children.Clear();
        _body.Children.Add(new TextBlock { Text = "Passo " + (step + 1) + " de 3", FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
        switch (step)
        {
            case 0:
                Title("Bem-vindo ao Urbe");
                Text("Suas notas são arquivos Markdown numa pasta sua, que você pode abrir em qualquer programa. Escolha a pasta das notas, ou crie uma nova pelo seletor.");
                Text("Dica: guarde uma cópia da pasta de vez em quando (Configurações → Exportar tudo).");
                Buttons(("Escolher pasta", true, async () =>
                {
                    if (ChooseFolder is not null && await ChooseFolder()) Show(1);
                }), ("Agora não", false, () => { Finish(false); return Task.CompletedTask; }));
                break;
            case 1:
                Title("Aparência");
                Text("Escolha um tema. Ele fica em Personalização/tema.json, na sua pasta, e dá para mudar tudo depois em Personalização.");
                var themes = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var (id, name, light, colors) in LegacyCustomizeTheme.PresetData)
                {
                    var bg = colors.First(c => c.Key == "fundo").Hex;
                    var accent = colors.First(c => c.Key == "destaque").Hex;
                    var text = colors.First(c => c.Key == "texto").Hex;
                    var chip = new Button
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Horizontal, Spacing = 8,
                            Children = { new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = UrbeTheme.Brush(accent) }, new TextBlock { Text = name, Foreground = UrbeTheme.Brush(text), VerticalAlignment = VerticalAlignment.Center } }
                        },
                        Background = UrbeTheme.Brush(bg), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 8, 8), MinHeight = 44
                    };
                    AutomationProperties.SetName(chip, "Tema " + name);
                    var theme = id;
                    chip.Click += async (_, _) => { if (ChooseTheme is not null) await ChooseTheme(theme); Show(2); };
                    themes.Children.Add(chip);
                }
                _body.Children.Add(themes);
                Buttons(("Manter o atual", false, () => { Show(2); return Task.CompletedTask; }));
                break;
            default:
                Title("Assistente");
                Text("O Assistente pesquisa, escreve e organiza suas notas com um modelo de IA que você escolhe. A chave fica só neste aparelho, nunca na pasta das notas.");
                Buttons(("Conectar um modelo", true, () => { Finish(true); return Task.CompletedTask; }),
                    ("Deixar para depois", false, () => { Finish(false); return Task.CompletedTask; }));
                break;
        }
    }

    private void Finish(bool connect)
    {
        IsVisible = false;
        Finished?.Invoke(connect);
    }

    private void Title(string text) =>
        _body.Children.Add(new TextBlock { Text = text, FontSize = 28, FontWeight = FontWeight.Bold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) });

    private void Text(string text) =>
        _body.Children.Add(new TextBlock { Text = text, FontSize = 16, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), LineHeight = 24 });

    private void Buttons(params (string Label, bool Primary, Func<Task> Run)[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var (label, primary, run) in buttons)
        {
            var b = new Button
            {
                Content = label, MinHeight = 46, Padding = new Thickness(18, 10), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(0), FontSize = 15,
                Background = primary ? UrbeTheme.Brush(UrbeTheme.Accent) : UrbeTheme.Brush(UrbeTheme.Surface2),
                Foreground = primary ? UrbeTheme.Brush(UrbeTheme.Bg) : UrbeTheme.Brush(UrbeTheme.Text)
            };
            AutomationProperties.SetName(b, label);
            b.Click += async (_, _) => await run();
            row.Children.Add(b);
        }
        _body.Children.Add(row);
    }
}
