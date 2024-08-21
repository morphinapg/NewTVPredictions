using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NewTVPredictions;

public partial class Predictions : UserControl
{
    public Predictions()
    {
        InitializeComponent();
    }

    private void DataGrid_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is DataGrid d)
            d.SelectedItem = null;
    }
}