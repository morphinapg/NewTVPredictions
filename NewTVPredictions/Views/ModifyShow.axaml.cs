using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NewTVPredictions;

public partial class ModifyShow : UserControl
{
    public ModifyShow()
    {
        InitializeComponent();
    }

    private void ListBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (sender is ListBox l)
            l.SelectedItem = null;
    }
}