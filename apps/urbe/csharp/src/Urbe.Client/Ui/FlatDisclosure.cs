using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client.Ui;

/// <summary>A single touchable line, with no Fluent nested surfaces. Content remains lazy and mounted.</summary>
/// <remarks>
/// CST-112: a run's history can hold hundreds of these. Each one is a plain toggle (a border and its content, no Fluent
/// template with visual states) and the parts follow the expander by events, not bindings: opening 300 stages cost a
/// frozen second mostly in applying templates.
/// </remarks>
public static class FlatDisclosure
{
    private static readonly FuncControlTemplate<ToggleButton> PlainToggle = new((toggle, scope) =>
    {
        var presenter = new ContentPresenter { Name = "PART_ContentPresenter" };
        presenter.Bind(ContentPresenter.ContentProperty, toggle.GetObservable(ContentControl.ContentProperty));
        presenter.Bind(ContentPresenter.BackgroundProperty, toggle.GetObservable(TemplatedControl.BackgroundProperty));
        presenter.Bind(ContentPresenter.PaddingProperty, toggle.GetObservable(TemplatedControl.PaddingProperty));
        presenter.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        presenter.VerticalContentAlignment = VerticalAlignment.Center;
        return presenter.RegisterInNameScope(scope);
    });

    public static Expander Create(Control header, bool showArrow = true)
    {
        var fold = new Expander { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        fold.Template = new FuncControlTemplate<Expander>((owner, _) =>
        {
            var label = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, IsHitTestVisible = false,
                Content = owner.Header };
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions(showArrow ? "*,24" : "*") };
            line.Children.Add(label);
            TextBlock? arrow = null;
            if (showArrow)
            {
                arrow = new TextBlock { Text = owner.IsExpanded ? "⌃" : "⌄", FontSize = 18,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0), IsHitTestVisible = false };
                Grid.SetColumn(arrow, 1);
                line.Children.Add(arrow);
            }
            var toggle = new ToggleButton { Content = line, MinHeight = UrbeTokens.MinTouch, Template = PlainToggle,
                Padding = new Thickness(0, 4), HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = new Cursor(StandardCursorType.Hand),
                IsChecked = owner.IsExpanded };
            var hover = UrbeTheme.Brush(UrbeTheme.Surface2);
            var content = new ContentControl { Margin = new Thickness(0, 4, 0, 8), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = owner.Content, IsVisible = owner.IsExpanded };
            toggle.PropertyChanged += (_, e) =>
            {
                if (e.Property == ToggleButton.IsCheckedProperty) owner.IsExpanded = toggle.IsChecked == true;
                else if (e.Property == InputElement.IsPointerOverProperty) toggle.Background = toggle.IsPointerOver ? hover : Brushes.Transparent;
            };
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == Expander.IsExpandedProperty)
                {
                    toggle.IsChecked = owner.IsExpanded;
                    content.IsVisible = owner.IsExpanded;
                    if (arrow is not null) arrow.Text = owner.IsExpanded ? "⌃" : "⌄";
                }
                else if (e.Property == ContentControl.ContentProperty) content.Content = owner.Content;
                else if (e.Property == HeaderedContentControl.HeaderProperty) label.Content = owner.Header;
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
