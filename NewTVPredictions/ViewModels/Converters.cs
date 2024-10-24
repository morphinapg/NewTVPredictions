using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels
{
    internal class ChangeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                doubleValue = Math.Round(doubleValue, 0);

                if (doubleValue > 0)
                {
                    return Brushes.Green;
                }
                else if (doubleValue < 0)
                {
                    return Brushes.Red;
                }
                else
                {
                    return Brushes.Transparent;
                }
            }

            return Brushes.Transparent; // Default to transparent if the value is not a double
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class ChangeConverter2 : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                doubleValue = Math.Round(doubleValue, 2);

                if (doubleValue > 0)
                {
                    return Brushes.Green;
                }
                else if (doubleValue < 0)
                {
                    return Brushes.Red;
                }
                else
                {
                    return Brushes.Transparent;
                }
            }

            return Brushes.Transparent; // Default to transparent if the value is not a double
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class ChangeConverter3 : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                doubleValue = Math.Round(doubleValue, 3);

                if (doubleValue > 0)
                {
                    return Brushes.Green;
                }
                else if (doubleValue < 0)
                {
                    return Brushes.Red;
                }
                else
                {
                    return Brushes.Transparent;
                }
            }

            return Brushes.Transparent; // Default to transparent if the value is not a double
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PerformanceToArrowConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double performance)
            {
                performance = Math.Round(performance, 0);

                if (performance > 0)
                {
                    return "↑";
                }
                else if (performance < 0)
                {
                    return "↓";
                }
                else
                    return "";
            }
            return ""; // Return an empty string for zero or non-double values
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PerformanceToArrowConverter2 : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double performance)
            {
                performance = Math.Round(performance, 2);

                if (performance > 0)
                {
                    return "↑";
                }
                else if (performance < 0)
                {
                    return "↓";
                }
                else
                    return "";
            }
            return ""; // Return an empty string for zero or non-double values
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PerformanceToArrowConverter3 : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double performance)
            {
                performance = Math.Round(performance, 3);

                if (performance > 0)
                {
                    return "↑";
                }
                else if (performance < 0)
                {
                    return "↓";
                }
                else
                    return "";
            }
            return ""; // Return an empty string for zero or non-double values
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
