using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NewTVPredictions.ViewModels;
using System.Linq;

namespace NewTVPredictions.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void ListBox_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is ListBox listbox)
        {
            var flyout = Summer.Flyout;
            flyout?.Hide();
        }
    }
}
