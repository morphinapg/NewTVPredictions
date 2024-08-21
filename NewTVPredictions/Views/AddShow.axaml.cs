using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NewTVPredictions.ViewModels;

namespace NewTVPredictions;

public partial class AddShow : UserControl
{
    public AddShow()
    {
        InitializeComponent();

        ShowGrid.DataContextChanged += ShowGrid_DataContextChanged;
    }

    private void ShowGrid_DataContextChanged(object? sender, System.EventArgs e)
    {
        NameBox.Focus();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        NameBox.Focus();
    }

    private void ListBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is ListBox l)
            l.SelectedItem = null;
    }
}