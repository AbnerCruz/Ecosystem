using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client.Ui;

/// <summary>A single touchable line, with no Fluent nested surfaces. Content remains lazy and mounted.</summary>
public static class FlatDisclosure
{
    public static Expander Create(Control header)
    {
        var fold = new Expander { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        fold.Template = new FuncControlTemplate<Expander>((owner, _) =>
        {
            var label = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
            label.Bind(ContentControl.ContentProperty, new Binding(nameof(Expander.Header)) { Source = owner });
            var arrow = new TextBlock { Text = owner.IsExpanded ? "⌃" : "⌄", FontSize = 18,
                Foreground = UrbeTheme.Brush(UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0), IsHitTestVisible = false };
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,24") };
            Grid.SetColumn(arrow, 1);
            line.Children.Add(label);
            line.Children.Add(arrow);
            var toggle = new ToggleButton { Content = line, MinHeight = UrbeTokens.MinTouch,
                Padding = new Thickness(0, 4), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(0) };
            toggle.Resources["ToggleButtonBackgroundPointerOver"] = UrbeTheme.Brush(UrbeTheme.Surface2);
            toggle.Resources["ToggleButtonBackgroundChecked"] = Brushes.Transparent;
            toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(Expander.IsExpanded))
                { Source = owner, Mode = BindingMode.TwoWay });
            var content = new ContentControl { Margin = new Thickness(0, 4, 0, 8),
                HorizontalContentAlignment = HorizontalAlignment.Stretch };
            content.Bind(ContentControl.ContentProperty, new Binding(nameof(Expander.Content)) { Source = owner });
            content.Bind(Control.IsVisibleProperty, new Binding(nameof(Expander.IsExpanded)) { Source = owner });
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == Expander.IsExpandedProperty) arrow.Text = owner.IsExpanded ? "⌃" : "⌄";
            };
            var rows = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
            Grid.SetRow(content, 1);
            rows.Children.Add(toggle);
            rows.Children.Add(content);
            return rows;
        });
        return fold;
    }
}
