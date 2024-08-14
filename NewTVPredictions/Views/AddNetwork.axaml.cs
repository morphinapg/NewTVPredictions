using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NewTVPredictions.ViewModels;

namespace NewTVPredictions;

public partial class AddNetwork : UserControl
{
    public AddNetwork()
    {
        InitializeComponent();
    }    

    Network? OldNetwork;

    private void Grid_DataContextChanged(object? sender, System.EventArgs e)                //subscribe to FactorFocused event when CurrentNetwork context changes
    {
        if (OldNetwork is not null)
        {
            OldNetwork.FactorFocused -= Network_FactorFocused;
        }

        var network = NetworkGrid.DataContext as Network;

        if (network is not null)
        {
            network.FactorFocused += Network_FactorFocused;
            OldNetwork = network;
        }

        NameBox.Focus();
    }

    private void Network_FactorFocused(object? sender, System.EventArgs e)                  //When a new factor is added to the network, focus the FactorBox
    {
        FactorBox.Focus();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        NameBox.Focus();
    }
}