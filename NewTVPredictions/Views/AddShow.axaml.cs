using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NewTVPredictions.ViewModels;

namespace NewTVPredictions;

public partial class AddShow : UserControl
{
    public AddShow()
    {
        InitializeComponent();

        NetworkPanel.DataContextChanged += NetworkPanel_DataContextChanged;
    }

    private void NetworkPanel_DataContextChanged(object? sender, System.EventArgs e)            //As soon as the NetworkPanel UserControl gains DataContext (the currently selected network) set the new show's Parent to the network.
    {
        if (ShowGrid.DataContext is Show s && NetworkPanel.DataContext is Network n)
            s.Parent = n;
    }
}