using System;
using System.Windows;
using System.Windows.Media;

namespace AIAgentTest.Services
{
    public enum ThemeType
    {
        Light,
        Dark
    }

    public class ThemeService
    {
        // Event to notify subscribers of theme changes
        public event EventHandler<ThemeType> ThemeChanged;

        public ThemeType CurrentTheme { get; private set; }

        public ThemeService()
        {
            // Default to light theme
            CurrentTheme = ThemeType.Light;
        }

        public void SetTheme(ThemeType theme)
        {
            CurrentTheme = theme;
            
            var resources = Application.Current.Resources;
            
            if (theme == ThemeType.Light)
            {
                // Set light theme colors
                resources["WindowBackground"] = new SolidColorBrush(Colors.White);
                resources["TextColor"] = new SolidColorBrush(Colors.Black);
                resources["MenuBackground"] = new SolidColorBrush(Colors.WhiteSmoke);
                resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                resources["ControlBackground"] = new SolidColorBrush(Colors.White);
                resources["SelectionBackground"] = new SolidColorBrush(Color.FromRgb(173, 216, 230));
                resources["HyperlinkColor"] = new SolidColorBrush(Color.FromRgb(0, 102, 204));
                resources["MenuItemSelectedBackground"] = new SolidColorBrush(Color.FromRgb(229, 229, 229));
                resources["ComboBoxBackground"] = new SolidColorBrush(Colors.White);
            }
            else
            {
                // Set dark theme colors
                resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                resources["TextColor"] = new SolidColorBrush(Colors.White);
                resources["MenuBackground"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                resources["ControlBackground"] = new SolidColorBrush(Color.FromRgb(61, 61, 61));
                resources["SelectionBackground"] = new SolidColorBrush(Color.FromRgb(38, 79, 120));
                resources["HyperlinkColor"] = new SolidColorBrush(Color.FromRgb(86, 156, 214));
                resources["MenuItemSelectedBackground"] = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                resources["ComboBoxBackground"] = new SolidColorBrush(Color.FromRgb(61, 61, 61));
            }
            
            // Notify subscribers
            ThemeChanged?.Invoke(this, theme);
            
            // Save setting
            Properties.Settings.Default.IsLightTheme = theme == ThemeType.Light;
            Properties.Settings.Default.Save();
        }
    }
}