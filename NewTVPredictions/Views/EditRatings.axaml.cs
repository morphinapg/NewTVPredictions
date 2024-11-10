using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System.Collections.Generic;
using System.Linq;

namespace NewTVPredictions;

public partial class EditRatings : UserControl
{
    public EditRatings()
    {
        InitializeComponent();
    }

    private void DataGrid_PreparingCellForEdit(object? sender, Avalonia.Controls.DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }
}