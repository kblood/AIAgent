using System;
using System.Globalization;
using System.Windows.Data;

namespace AIAgentTest.Views
{
    /// <summary>
    /// Converts a boolean value to a string
    /// </summary>
    public class BooleanToStringConverter : IValueConverter
    {
        /// <summary>
        /// String to use when the value is true
        /// </summary>
        public string TrueValue { get; set; } = "True";
        
        /// <summary>
        /// String to use when the value is false
        /// </summary>
        public string FalseValue { get; set; } = "False";
        
        /// <summary>
        /// Convert a boolean to a string
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueValue : FalseValue;
            }
            
            return FalseValue;
        }
        
        /// <summary>
        /// Convert a string back to a boolean
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return stringValue == TrueValue;
            }
            
            return false;
        }
    }
}