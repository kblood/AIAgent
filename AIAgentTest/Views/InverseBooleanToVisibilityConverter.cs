using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AIAgentTest.Views
{
    /// <summary>
    /// Converts a boolean value to a Visibility value (inverted)
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Convert a boolean to a Visibility value (inverted)
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Visible;
        }
        
        /// <summary>
        /// Convert a Visibility value back to a boolean (inverted)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            
            return false;
        }
    }
}