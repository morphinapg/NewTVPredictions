using Avalonia.Controls;

namespace NewTVPredictions.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }
}
