using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AIAgentTest.Views
{
    // This class provides converters for MVVM binding
    public class Converters
    {
        // BooleanToVisibilityConverter for use in XAML
        public static BooleanToVisibilityConverter BooleanToVisibility { get; } = new BooleanToVisibilityConverter();
    }
    
    /// <summary>
    /// Converts boolean values to Visibility enum values
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check if we need to invert the result
                if (parameter is string strParam && strParam.ToLower() == "invert")
                {
                    boolValue = !boolValue;
                }
                
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                
                // Check if we need to invert the result
                if (parameter is string strParam && strParam.ToLower() == "invert")
                {
                    result = !result;
                }
                
                return result;
            }
            
            return false;
        }
    }
}