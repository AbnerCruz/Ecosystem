using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Urbe.Client;

public sealed partial class UrbeDialogs
{
    public Task<string?> ChooseFileAsync(IEnumerable<string> paths) => Open<string?>(finish =>
    {
        var files = paths.Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var search = new TextBox { PlaceholderText = "Buscar arquivo pelo nome ou caminho" };
        var list = new ListBox { ItemsSource = files, Height = 260, Margin = new Thickness(0, 8) };
        search.TextChanged += (_, _) => list.ItemsSource = files.Where(p => p.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is string file) finish(file); };
        var layout = new StackPanel { Children = { Head("Escolher arquivo", "Todos os tipos de arquivo", () => finish(null)), search, list } };
        return (layout, null, null, search);
    });
}
