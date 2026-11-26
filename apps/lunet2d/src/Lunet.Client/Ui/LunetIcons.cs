using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Lunet.Client.Ui;

/// <summary>
/// Stroke icons on a 24×24 grid (same drawing style as the Numa family). Vector only: no Unicode glyphs as icons,
/// which the old shell mixed in and which render differently on each device.
/// </summary>
public static class LunetIcons
{
    public const string Back = "M15 18l-6-6 6-6";
    public const string Plus = "M12 5v14 M5 12h14";
    public const string Play = "M7 4.5v15l12-7.5z";
    public const string Pause = "M8 5v14 M16 5v14";
    public const string Music = "M9 18V5l11-2v13 M9 18a3 3 0 1 1-6 0 3 3 0 0 1 6 0z M20 16a3 3 0 1 1-6 0 3 3 0 0 1 6 0z";
    public const string Sparkle = "M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z M19 16l.7 1.8 1.8.7-1.8.7-.7 1.8-.7-1.8-1.8-.7 1.8-.7z";
    public const string Speaker = "M4 9.5h3.5L12 5.5v13l-4.5-4H4z M15.5 9a4 4 0 0 1 0 6 M18 6.5a7.5 7.5 0 0 1 0 11";
    public const string Star = "M12 3.5l2.6 5.3 5.8.8-4.2 4.1 1 5.8-5.2-2.7-5.2 2.7 1-5.8-4.2-4.1 5.8-.8z";
    public const string Stop = "M6.5 6.5h11v11h-11z";
    public const string Step = "M6 5v14l9-7z M18 5v14";
    public const string Code = "M9 7l-5 5 5 5 M15 7l5 5-5 5";
    public const string Game = "M6 9h12a3 3 0 0 1 3 3v2a3 3 0 0 1-5.2 2.1L14 14h-4l-1.8 2.1A3 3 0 0 1 3 14v-2a3 3 0 0 1 3-3z M8 11v3 M6.5 12.5h3 M16 12h.01 M18 13.5h.01";
    public const string Art = "M12 3a9 9 0 1 0 0 18c1.2 0 1.8-.9 1.8-1.8 0-1.1-.8-1.4-.8-2.4 0-.9.7-1.6 1.6-1.6H17a4 4 0 0 0 4-4C21 6.6 17 3 12 3z M7.5 11h.01 M10 7.5h.01 M14.5 7.5h.01";
    public const string Map = "M3 6l6-3 6 3 6-3v15l-6 3-6-3-6 3z M9 3v15 M15 6v15";
    public const string Sound = "M9 18V6l11-2v12 M9 18a3 3 0 1 1-6 0a3 3 0 1 1 6 0z M20 16a3 3 0 1 1-6 0a3 3 0 1 1 6 0z";
    public const string Notes = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z M14 3v5h5 M9 13h6M9 17h4";
    public const string Console = "M4 5h16v14H4z M7.5 9.5l3 2.5-3 2.5 M12.5 15h4";
    public const string Problems = "M12 4l9 16H3z M12 10v4 M12 17h.01";
    public const string Folder = "M3.5 7.5a2 2 0 0 1 2-2h3.8l2 2h7.2a2 2 0 0 1 2 2v7.5a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z";
    public const string File = "M14 3.5H7.5a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h9a2 2 0 0 0 2-2V8z M14 3.5V8h4.5";
    public const string Close = "M6 6l12 12 M18 6L6 18";
    public const string Keyboard = "M3.5 6.5h17a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-17a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1z M6.5 10h.01 M10 10h.01 M14 10h.01 M17.5 10h.01 M8 14h8";
    public const string Braces = "M8 4H7a2 2 0 0 0-2 2v3a2 2 0 0 1-2 2 2 2 0 0 1 2 2v3a2 2 0 0 0 2 2h1 M16 4h1a2 2 0 0 1 2 2v3a2 2 0 0 0 2 2 2 2 0 0 0-2 2v3a2 2 0 0 1-2 2h-1";
    public const string Function = "M15.5 4.5c-2.5-.8-4 .4-4.5 3l-2 9.5c-.5 2.6-2 3.8-4.5 3 M7.5 10h8";
    public const string Search = "M11 4a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M20 20l-4-4";
    public const string More = "M12 5.5h.01 M12 12h.01 M12 18.5h.01";
    /// <summary>Sliders: the Inspector of the running game (M6).</summary>
    public const string Inspector = "M4 6h9 M17 6h3 M15 4v4 M4 12h3 M11 12h9 M9 10v4 M4 18h11 M19 18h1 M17 16v4";
    /// <summary>A pulse: the Desempenho panel (M6).</summary>
    public const string Pulse = "M3 12h4l3-7 4 14 3-7h4";
    public const string Trash = "M4 7h16 M10 11v6 M14 11v6 M6 7l1 13h10l1-13 M9 7V4h6v3";
    public const string Edit = "M4 20h4l10.5-10.5-4-4L4 16z M13.5 6.5l4 4";
    public const string Copy = "M9 9h11v11H9z M5 15H4V4h11v1";
    public const string Share = "M12 15V3 M7 8l5-5 5 5 M5 14v5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5";
    public const string Download = "M12 3v12 M7 10l5 5 5-5 M5 14v5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5";
    public const string Home = "M4 11l8-7 8 7 M6 9.5V20h12V9.5 M10 20v-5h4v5";
    public const string Book = "M5 4.5h9a3 3 0 0 1 3 3V20H8a3 3 0 0 1-3-3z M5 17a3 3 0 0 1 3-3h9";
    public const string ChevronDown = "M6 9l6 6 6-6";
    public const string Check = "M5 12.5l4.5 4.5L19 7.5";
    public const string Eraser = "M7 20h11 M5.5 15.5l8-8 5 5-5.5 5.5H9z M10.5 10.5l5 5";
    public const string Bucket = "M5 11l7-7 7 7-7 7z M5 11h14 M20 15c0 1.5-1 2.5-1 3.5a1 1 0 0 0 2 0c0-1-1-2-1-3.5z";
    public const string Dropper = "M15 4l5 5-2 2-5-5z M13 6l-8 8v3h3l8-8";
    public const string Fit = "M4 9V4h5 M20 9V4h-5 M4 15v5h5 M20 15v5h-5";
    public const string ChevronRight = "M9 6l6 6-6 6";
    public const string Undo = "M9 14L4 9l5-5 M4 9h10a6 6 0 0 1 0 12h-3";
    public const string Select = "M4 7V4h3 M10 4h4 M17 4h3v3 M20 10v4 M20 17v3h-3 M14 20h-4 M7 20H4v-3 M4 14v-4";
    public const string Lasso = "M12 4c5 0 8 2.2 8 5s-3.6 5-8 5-8-2.2-8-5 3.6-5 8-5z M7 13.5c-1.5 2-.5 4.2 1.5 4.5 M8.5 18v3";
    public const string Wand = "M4 20L15 9 M17 3v3 M20.5 7.5h-3 M19.5 4.5l-2 2 M13 5h.01 M20 12h.01";
    public const string Rotate = "M20 12a8 8 0 1 1-2.3-5.7 M20 4v5h-5";
    public const string FlipH = "M12 3v18 M9 7L3 17h6z M15 7l6 10h-6z";
    public const string FlipV = "M3 12h18 M7 9L17 3v6z M7 15l10 6v-6z";
    public const string Redo = "M15 14l5-5-5-5 M20 9H10a6 6 0 0 0 0 12h3";
    public const string Speed = "M12 13l4-4 M4 17a8 8 0 1 1 16 0";

    /// <summary>An icon as a stroked path, sized for a toolbar (20 dp) unless told otherwise.</summary>
    public static Control Create(string data, string color = LunetTokens.Color.TextPrimary, double size = 20, bool filled = false)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = StreamGeometry.Parse(data),
            Stroke = LunetTokens.Brush(color),
            StrokeThickness = 1.75, // the icon stroke of the visual language (§7)
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Fill = filled ? LunetTokens.Brush(color) : null,
            Stretch = Stretch.None,
        };
        return new Viewbox { Width = size, Height = size, Child = new Canvas { Width = 24, Height = 24, Children = { path } } };
    }
}
