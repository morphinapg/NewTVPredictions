using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NewTVPredictions;

public partial class PredictionAccuracy : UserControl
{
    public PredictionAccuracy()
    {
        InitializeComponent();
    }

    private void DataGrid_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is DataGrid d)
            d.SelectedItem = null;
    }
}