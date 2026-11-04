using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client;

public sealed record ImageStudyQuote(string Path, int? Page, string Text, string Sha256, string Region, bool Reviewed, int Rotation=0, string Language="por+eng");
public sealed record StudyImageQuestion(string Path,string Sha256,string Region,byte[] Png);

/// <summary>Native region selection, explicit local recognition and editable review before an ordinary note citation.</summary>
public sealed class OcrStudyView : Grid, IDisposable
{
    private readonly string _path, _hash;
    private readonly int? _page;
    private readonly byte[] _imageBytes;
    private readonly Image _image;
    private readonly Canvas _selectionCanvas = new() { Background = Brushes.Transparent };
    private readonly Border _rectangle = new() { BorderBrush = Brushes.DodgerBlue, BorderThickness = new Thickness(2), Background = new SolidColorBrush(Color.FromArgb(35, 30, 144, 255)), IsHitTestVisible = false };
    private readonly ContentControl _body = new();
    private readonly TextBox _text = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
    private readonly TextBlock _status = new() { Text = "Arraste sobre a imagem para selecionar uma região ou reconheça a página inteira.", TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _reviewed = new() { Content = "Conferi o texto na imagem original", MinHeight = 48 };
    private readonly Ui.UrbeButton _run = new("Reconhecer região", Ui.ButtonKind.Primary, UrbeTheme.Icons.Scan), _save = new("Adicionar à nota", Ui.ButtonKind.Primary);
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMilliseconds(750) };
    private Point? _start;
    private Rect _region = new(0, 0, 1, 1);
    private IReaderComputation? _engine;
    private bool _closed, _reading;
    private int _rotation;
    private readonly ComboBox _language=new(){ItemsSource=new[]{"Português + English","Português","English"},SelectedIndex=0,MinHeight=48};
    private string _usedLanguage="por+eng";
    private int _usedRotation;
    private Control? _proof;
    private string _usedRegion = "0,0,1,1";
    public event Action<ImageStudyQuote>? QuoteRequested;
    public event Action<StudyImageQuestion>? VisualQuestionRequested;
    public event Action? CloseRequested;

    public OcrStudyView(string path, int? page, string hash, byte[] imageBytes, ReaderRegion? initialRegion=null)
    {
        if(initialRegion is { } initial)_region=new Rect(initial.X,initial.Y,initial.Width,initial.Height);
        _path = path; _page = page; _hash = hash; _imageBytes = imageBytes;
        using var stream = new MemoryStream(imageBytes); _image = new Image { Source = new Bitmap(stream), Stretch = Stretch.Uniform };
        RowDefinitions = new RowDefinitions("Auto,*,Auto");
        var back = new Ui.UrbeIconButton(UrbeTheme.Icons.Back, "Voltar à fonte"); back.Click += (_, _) => CloseRequested?.Invoke();
        _run.Click += async (_, _) => await RecognizeAsync();
        var compare = new Ui.UrbeIconButton(UrbeTheme.Icons.Image, "Imagem / Texto", 21);
        compare.Click += (_, _) => { if (_proof is not null && _save.IsEnabled) { _proof.IsVisible = !_proof.IsVisible; _text.IsVisible = !_proof.IsVisible; } };
        var rotate = new Ui.UrbeIconButton(UrbeTheme.Icons.Restore, "Orientação OCR: 0°", 21);
        rotate.Click += (_, _) =>
        {
            _rotation = (_rotation + 90) % 360;
            ToolTip.SetTip(rotate, "Orientação OCR: " + _rotation + "°");
            Avalonia.Automation.AutomationProperties.SetName(rotate, "Orientação OCR: " + _rotation + "°");
            _status.Text = "O reconhecimento vai ler a imagem girada " + _rotation + "°.";
        };
        var ask = new Ui.UrbeIconButton(UrbeTheme.Icons.Assistant, "Perguntar sobre a imagem", 21);
        ask.Click+=async(_,_)=>
        {
            ask.IsEnabled=false;
            try{var region=_region;byte[] png=await Task.Run(()=>Crop(_imageBytes,region));if(!_closed)VisualQuestionRequested?.Invoke(new(_path,_hash,RegionText(region),png));}
            finally{ask.IsEnabled=true;}
        };
        _language.MinHeight = 40;
        _language.VerticalAlignment = VerticalAlignment.Center;
        _run.MinHeight = 40;
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { back, _run, compare, rotate, ask, _language } };
        _status.FontSize = 12;
        _status.Margin = new Thickness(4, 6, 4, 2);
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        var header = new StackPanel { Children = { new Border
        {
            CornerRadius = new CornerRadius(UrbeTokens.Radius.L), Background = UrbeTheme.Brush(UrbeTheme.Surface2), Padding = new Thickness(4, 0),
            Child = new ScrollViewer
            {
                Content = controls,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            }
        }, _status } }; Children.Add(header);
        var source = new Grid { Children = { _image, _selectionCanvas } };
        _selectionCanvas.Children.Add(_rectangle); _rectangle.IsVisible = false;
        _selectionCanvas.PointerPressed += (_, e) =>
        {
            if (!_run.IsEnabled) return;
            var pos = Normalized(e.GetPosition(_image)); if (pos is null) return;
            _start = pos; e.Pointer.Capture(_selectionCanvas); e.Handled = true;
        };
        _selectionCanvas.PointerMoved += (_, e) =>
        {
            if (_start is not { } start || Normalized(e.GetPosition(_image), true) is not { } end) return;
            _region = new Rect(start, end); DrawRegion(); e.Handled = true;
        };
        _selectionCanvas.PointerReleased += (_, e) => { _start = null; e.Pointer.Capture(null); e.Handled = true; };
        source.SizeChanged += (_, _) => DrawRegion();
        _body.Content = source; SetRow(_body, 1); Children.Add(_body);
        _save.IsEnabled = false;
        _save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_text.Text)) return;
            QuoteRequested?.Invoke(new(_path, _page, _text.Text!, _hash, _usedRegion, _reviewed.IsChecked == true,_usedRotation,_usedLanguage));
        };
        var footer = new StackPanel { Children = { _reviewed, _save } }; SetRow(footer, 2); Children.Add(footer);
        _poll.Tick += async (_, _) => await PollAsync();
        DetachedFromVisualTree += (_, _) => Dispose();
    }
    private Rect ImageRect()
    {
        if (_image.Source is not Bitmap bitmap) return default;
        var b = _image.Bounds; double scale = Math.Min(b.Width / bitmap.PixelSize.Width, b.Height / bitmap.PixelSize.Height);
        var size = new Size(bitmap.PixelSize.Width * scale, bitmap.PixelSize.Height * scale);
        return new Rect((b.Width - size.Width) / 2, (b.Height - size.Height) / 2, size.Width, size.Height);
    }
    private Point? Normalized(Point point, bool clamp = false)
    {
        var rect = ImageRect(); if (rect.Width <= 0 || rect.Height <= 0 || !clamp && !rect.Contains(point)) return null;
        return new Point(Math.Clamp((point.X - rect.X) / rect.Width, 0, 1), Math.Clamp((point.Y - rect.Y) / rect.Height, 0, 1));
    }
    private void DrawRegion()
    {
        var rect = ImageRect(); _rectangle.IsVisible = _region.Width > .005 && _region.Height > .005;
        Canvas.SetLeft(_rectangle, rect.X + _region.X * rect.Width); Canvas.SetTop(_rectangle, rect.Y + _region.Y * rect.Height);
        _rectangle.Width = _region.Width * rect.Width; _rectangle.Height = _region.Height * rect.Height;
    }
    private static string RegionText(Rect region)=>string.Join(",",new[]{region.X,region.Y,region.Width,region.Height}.Select(n=>n.ToString("0.######",CultureInfo.InvariantCulture)));
    public static byte[] Rotate(byte[] source,int rotation)
    {
        if(rotation==0)return source;
        if(rotation is not (90 or 180 or 270))throw new ArgumentOutOfRangeException(nameof(rotation));
        using var input=SKBitmap.Decode(source);using var output=new SKBitmap(rotation==180?input.Width:input.Height,rotation==180?input.Height:input.Width);
        using(var canvas=new SKCanvas(output)){canvas.Clear(SKColors.White);canvas.Translate(output.Width/2f,output.Height/2f);canvas.RotateDegrees(rotation);canvas.DrawBitmap(input,-input.Width/2f,-input.Height/2f);}
        using var png=output.Encode(SKEncodedImageFormat.Png,100);return png.ToArray();
    }
    public static byte[] Crop(byte[] source, Rect normalized)
    {
        using var input = SKBitmap.Decode(source) ?? throw new InvalidDataException("Imagem inválida para OCR.");
        int x = Math.Clamp((int)(normalized.X * input.Width), 0, input.Width - 1), y = Math.Clamp((int)(normalized.Y * input.Height), 0, input.Height - 1);
        int width = Math.Clamp((int)Math.Ceiling(normalized.Width * input.Width), 1, input.Width - x), height = Math.Clamp((int)Math.Ceiling(normalized.Height * input.Height), 1, input.Height - y);
        using var output = new SKBitmap(width, height); using (var canvas = new SKCanvas(output)) canvas.DrawBitmap(input, -x, -y);
        using var png = output.Encode(SKEncodedImageFormat.Png, 100); return png.ToArray();
    }
    private async Task RecognizeAsync()
    {
        _run.IsEnabled = false; _status.Text = "Preparando OCR local…";
        try
        {
            var region = _region.Width > .005 && _region.Height > .005 ? _region : new Rect(0, 0, 1, 1);
            _usedRegion=RegionText(region);_usedRotation=_rotation;_usedLanguage=_language.SelectedIndex switch{1=>"por",2=>"eng",_=>"por+eng"};
            byte[] crop = await Task.Run(() => Rotate(Crop(_imageBytes, region),_usedRotation));
            string html = await Task.Run(() => OcrReaderHtml.Build(crop,_usedLanguage));
            if (_closed) return;
            var control = await ContentViewers.CreateAsync(new ContentRequest(null, _path, "OCR local", VaultFileKinds.Html, Encoding.UTF8.GetBytes(html))
            { ReaderNavigate = _ => { }, ReaderEngine = ReaderContentEngine.Ocr });
            if (_closed) { (control as IDisposable)?.Dispose(); return; }
            if (control is not IReaderComputation computation) { _status.Text = "O host de OCR não está disponível neste dispositivo."; _run.IsEnabled = true; return; }
            _engine = computation; _proof = control; _text.IsVisible = false; _body.Content = new Grid { Children = { control, _text } }; _poll.Start();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _status.Text = "Não foi possível iniciar o OCR: " + ex.Message; _run.IsEnabled = true; }
    }
    private async Task PollAsync()
    {
        if (_closed || _reading || _engine is null) return;
        _reading = true;
        try
        {
            using var json = JsonDocument.Parse(await _engine.ReadComputationAsync());
            if (_closed || json.RootElement.ValueKind != JsonValueKind.Object) return;
            var value = json.RootElement; var status = value.GetProperty("status").GetString();
            if (status == "done")
            {
                _poll.Stop(); _text.Text = value.GetProperty("text").GetString();
                double confidence = value.GetProperty("confidence").GetDouble();
                _status.Text = $"OCR estimado · confiança do motor {confidence:0}% (não é garantia de exatidão). Revise acentos, números e fórmulas antes de citar.";
                if (_proof is not null) _proof.IsVisible = false;
                _text.IsVisible = true; _save.IsEnabled = true;
            }
            else if (status == "error") { _poll.Stop(); _status.Text = "OCR indisponível: " + value.GetProperty("error").GetString(); }
            else _status.Text = "Reconhecendo localmente… " + (value.TryGetProperty("progress", out var p) ? (p.GetDouble() * 100).ToString("0") + "%" : "");
        }
        catch (Exception ex) when (ex is TimeoutException or JsonException or InvalidOperationException or ObjectDisposedException) { if (!_closed) _status.Text = "Aguardando o motor local de reconhecimento…"; }
        finally { _reading = false; }
    }
    public void Dispose()
    {
        if (_closed) return; _closed = true; _poll.Stop();
        if (_image.Source is IDisposable image) image.Dispose();
        if (_body.Content is Control control) control.IsVisible = false;
        _body.Content = null; _engine = null;
    }
}
