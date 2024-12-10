using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAvalonia.Core;
using NewTVPredictions.ViewModels;
using System.Linq;

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

        bool RatingsGrid = false;
        if (control is Visual visual)
        {
            var ancestors = visual.GetVisualAncestors();
            var test1 = ancestors.Where(x => x is DataGridColumnHeader).Any();
            var test2 = ancestors.Select(x => x.Name).Contains("RatingsGrid");

            RatingsGrid = test1 && test2;
        }

        if (control is not null && control is not LightDismissOverlayLayer && control is not Border && !RatingsGrid)
            BeginMoveDrag(e);        
    }
}
