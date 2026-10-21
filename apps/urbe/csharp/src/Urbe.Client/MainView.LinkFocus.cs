using Avalonia.Input;
using Avalonia.Interactivity;
using Urbe.Client.World;

namespace Urbe.Client;

/// <summary>
/// UC-70, the links focus in the app: a tap on a note darkens the city around its links (WorldView) and this card
/// takes the place of the house card. "Abrir" opens the note, "Detalhes" (or a second tap on the building) shows the
/// 1.8.4 house card, a tap on a linked building shows its name with "Abrir" and "Focar". The focus ends with ✕, a tap
/// on empty ground, Back or Esc.
/// </summary>
public sealed partial class MainView
{
    /// <summary>The card of the links focus.</summary>
    public LinkFocusBar FocusBar { get; } = new();

    private void WireLinkFocus()
    {
        World.LinkFocusChanged += (_, _) =>
        {
            // a linked note tapped while the house card is open: back to the focus card, with its name
            if (World.Peeked is not null) ShowHouse(null);
            ShowFocusBar();
            UpdateNav();
        };
        World.FocusDetailsRequested += house => { FocusBar.IsVisible = false; ShowHouse(house); };
        FocusBar.OpenRequested += house => Editor.Open(new CityNote(house.Path, _contents.GetValueOrDefault(house.Path, "")));
        FocusBar.DetailsRequested += house => { FocusBar.IsVisible = false; ShowHouse(house); };
        FocusBar.FocusRequested += house => World.FocusLinks(house);
        FocusBar.CloseRequested += () => World.Select(null);
        // Esc on a computer; Back on Android (TopLevel.BackRequested, wired on attach)
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape && LeaveLinkFocus()) e.Handled = true;
        }, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) =>
        {
            if (Avalonia.Controls.TopLevel.GetTopLevel(this) is { } top) top.BackRequested += OnBackForFocus;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (Avalonia.Controls.TopLevel.GetTopLevel(this) is { } top) top.BackRequested -= OnBackForFocus;
        };
    }

    private void OnBackForFocus(object? sender, RoutedEventArgs e)
    {
        if (LeaveLinkFocus()) e.Handled = true;
    }

    /// <summary>Back/Esc: leaves the links focus when it is on and nothing covers the city. True when it did.</summary>
    public bool LeaveLinkFocus()
    {
        if (World.Linked is null || !World.CityVisible() || Dialogs.IsOpen) return false;
        World.Select(null);
        return true;
    }

    private void ShowFocusBar()
    {
        if (World.Linked is null) { FocusBar.Show(null, null, World.ArtOf); return; }
        if (_housePanel.IsVisible) return;   // the house card asked with "Detalhes" stays until closed
        FocusBar.Show(World.Linked, World.Peeked, World.ArtOf);
    }
}
