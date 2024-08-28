using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using NewTVPredictions.ViewModels;

namespace NewTVPredictions.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        CurrentApp.TopLevel = GetTopLevel(this);
    }

    private void Window_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var control = this.InputHitTest(e.GetPosition(this));

        if (control is not null && control is not LightDismissOverlayLayer && control is not Border)
            BeginMoveDrag(e);        
    }
}
