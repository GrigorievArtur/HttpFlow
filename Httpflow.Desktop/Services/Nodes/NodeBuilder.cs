using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Httpflow.Desktop.Models.Nodes;

namespace Httpflow.Desktop.Services;

public class NodeBuilder
{
    public Border Build(CanvasNodeRecord record)
    {
        var valuesPanel = new StackPanel
        {
            Spacing = 8
        };

        foreach (var value in record.Values)
        {
            valuesPanel.Children.Add(new Border
            {
                Padding = new Thickness(10, 8),
                Background = Brush.Parse("#FFF5DB"),
                CornerRadius = new CornerRadius(8),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    ColumnSpacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = value.Label,
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = value.Value,
                            TextAlignment = TextAlignment.Right,
                            [Grid.ColumnProperty] = 1
                        }
                    }
                }
            });
        }

        return new Border
        {
            Width = 220,
            Padding = new Thickness(14),
            Background = Brush.Parse("#FFFDF7"),
            BorderBrush = Brush.Parse("#1D1D1D"),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(10),
            BoxShadow = BoxShadows.Parse("0 8 24 0 #22000000"),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        ColumnSpacing = 10,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = record.Name,
                                FontSize = 16,
                                FontWeight = FontWeight.SemiBold
                            },
                            new Border
                            {
                                Padding = new Thickness(8, 3),
                                CornerRadius = new CornerRadius(999),
                                Background = Brush.Parse("#1D1D1D"),
                                Child = new TextBlock
                                {
                                    Text = record.NodeType,
                                    Foreground = Brushes.White,
                                    FontSize = 11
                                },
                                [Grid.ColumnProperty] = 1
                            }
                        }
                    },
                    valuesPanel
                }
            }
        };
    }
}
