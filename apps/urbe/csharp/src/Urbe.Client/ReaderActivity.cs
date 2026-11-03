using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Urbe.Client;

public interface IReaderActivity { void SetReaderActive(bool active); }
public static class ReaderActivity
{
    public static void SetActive(Control root, bool active)
    {
        if (root is IReaderActivity own) own.SetReaderActive(active);
        foreach (var child in root.GetVisualDescendants().OfType<IReaderActivity>()) child.SetReaderActive(active);
    }
    public const string PauseScript = "document.querySelectorAll('audio,video').forEach(m=>m.pause())";
}
