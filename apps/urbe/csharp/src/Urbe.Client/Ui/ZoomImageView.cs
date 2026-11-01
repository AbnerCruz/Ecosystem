using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Urbe.Client.Ui;

/// <summary>Native image inspection: fit, buttons, wheel and pinch; never opens a separate application.</summary>
public sealed class ZoomImageView : Grid
{
    private readonly Image _image;
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _percent;
    private Bitmap? _bitmap;
    private readonly Size _sourceSize;
    private double _pinch = 1;
    public double Zoom { get; private set; } = 1;

    public ZoomImageView(Func<Bitmap> decode)
    {
        ArgumentNullException.ThrowIfNull(decode);
        _bitmap = decode();
        _sourceSize = _bitmap.Size;
        RowDefinitions = new RowDefinitions("Auto,*");
        Height = 420;
        _image = new Image { Source = _bitmap, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _scroll = new ScrollViewer { Content = _image, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _percent = new TextBlock { Text = "100%", VerticalAlignment = VerticalAlignment.Center,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text2), FontSize = 12 };
        var actions = new WrapPanel { Children = { Action("−", () => SetZoom(Zoom / 1.25)), _percent,
            Action("+", () => SetZoom(Zoom * 1.25)), Action("Ajustar", () => { SetZoom(1); _scroll.Offset = default; }) } };
        Grid.SetRow(_scroll, 1);
        Children.Add(actions);
        Children.Add(_scroll);
        _scroll.SizeChanged += (_, _) => Resize();
        _scroll.GestureRecognizers.Add(new PinchGestureRecognizer());
        _scroll.AddHandler(InputElement.PinchEvent, (_, e) => { SetZoom(Zoom * e.Scale / _pinch); _pinch = e.Scale; e.Handled = true; });
        _scroll.AddHandler(InputElement.PinchEndedEvent, (_, _) => _pinch = 1);
        _scroll.PointerWheelChanged += (_, e) => { if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return; SetZoom(Zoom * Math.Pow(1.15, e.Delta.Y)); e.Handled = true; };
        // Detach releases native pixels, but a retained/reparented preview is reusable.
        // Layout only uses cached dimensions; it must never query a disposed bitmap.
        DetachedFromVisualTree += (_, _) =>
        {
            _image.Source = null;
            var bitmap = _bitmap;
            _bitmap = null;
            bitmap?.Dispose();
        };
        AttachedToVisualTree += (_, _) =>
        {
            if (_bitmap is not null) return;
            _bitmap = decode();
            _image.Source = _bitmap;
            Resize();
        };
    }

    public void SetZoom(double zoom)
    {
        Zoom = Math.Clamp(zoom, 1, 8);
        _percent.Text = Math.Round(Zoom * 100) + "%";
        Resize();
    }

    private void Resize()
    {
        var fit = Math.Min(Math.Max(1, _scroll.Bounds.Width), _sourceSize.Width);
        fit = Math.Min(fit, Math.Max(1, _scroll.Bounds.Height) * _sourceSize.Width / _sourceSize.Height);
        _image.Width = fit * Zoom;
        _image.Height = _image.Width * _sourceSize.Height / _sourceSize.Width;
    }

    private static Button Action(string title, System.Action click)
    {
        var button = new Button { Content = title, MinWidth = 48, MinHeight = 48,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = UrbeTheme.Brush(UrbeTheme.Accent) };
        Avalonia.Automation.AutomationProperties.SetName(button, title == "+" ? "Ampliar imagem" : title == "−" ? "Reduzir imagem" : "Ajustar imagem");
        button.Click += (_, _) => click();
        return button;
    }
}
