using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Avalonia.VisualTree;
using Avalonia.Media;

namespace NewTVPredictions.ViewModels
{
    public static class UserControlExtensions
    {
        public static void RenderToFile(this UserControl control, string path)
        {
            var charts = control.GetVisualDescendants().Where(x => x.Name == "PredictionChart");

            Visual chart = charts.Any() ? charts.First() : control;

            var oldBackground = control.Background;

            control.Background = new SolidColorBrush(new Color(255, 30, 30, 30));

            // Ensure the control is measured and arranged
            control.Measure(new Size(chart.Bounds.Width + 30, double.PositiveInfinity));
            control.Arrange(new Rect(control.DesiredSize));

            // Create a render target bitmap
            var pixelSize = new PixelSize((int)control.Bounds.Width, (int)control.Bounds.Height);
            var dpi = new Vector(96, 96);
            using var bitmap = new RenderTargetBitmap(pixelSize, dpi);

            // Render the control to the bitmap
            bitmap.Render(control);

            // Save the bitmap to a file
            using var stream = File.OpenWrite(path);
            bitmap.Save(stream);

            control.Background = oldBackground;
        }
    }
}
