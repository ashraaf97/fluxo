using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Fluxo.Wpf.UI
{
    /// <summary>
    /// Resolves an already-chosen icon name to its geometry.
    ///
    /// The FileExtension* converters derive the icon from a file name, which cannot
    /// work for a torrent's header row - that has a torrent name, not a file name,
    /// and wants a folder icon. Rows expose FileIconText instead, having already
    /// decided which icon they want, and these two just look it up.
    /// </summary>
    [ValueConversion(typeof(string), typeof(Geometry))]
    internal class VectorNameToGeometryConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            var name = value as string;
            return string.IsNullOrEmpty(name)
                ? null
                : Application.Current.TryFindResource(name);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    [ValueConversion(typeof(string), typeof(SolidColorBrush))]
    internal class VectorNameToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            var name = value as string;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            // Not every icon has a matching colour; those fall back to the list's
            // default icon brush rather than rendering invisible.
            return Application.Current.TryFindResource("color-" + name)
                ?? Application.Current.TryFindResource("ListViewIconForecolor");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
