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
                resources["ScrollBarBackground"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                resources["ScrollBarThumbBackground"] = new SolidColorBrush(Color.FromRgb(205, 205, 205));
                resources["CodeBackground"] = new SolidColorBrush(Color.FromRgb(248, 248, 248));
                resources["AccentColor"] = new SolidColorBrush(Color.FromRgb(0, 122, 204));
            }
            else
            {
                // Set dark theme colors
                resources["WindowBackground"] = new SolidColorBrush(Color.FromRgb(33, 33, 33));
                resources["TextColor"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                resources["MenuBackground"] = new SolidColorBrush(Color.FromRgb(24, 24, 24));
                resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(70, 70, 70));
                resources["ControlBackground"] = new SolidColorBrush(Color.FromRgb(50, 50, 50));
                resources["SelectionBackground"] = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                resources["HyperlinkColor"] = new SolidColorBrush(Color.FromRgb(77, 179, 255));
                resources["MenuItemSelectedBackground"] = new SolidColorBrush(Color.FromRgb(66, 66, 66));
                resources["ComboBoxBackground"] = new SolidColorBrush(Color.FromRgb(50, 50, 50));
                resources["ScrollBarBackground"] = new SolidColorBrush(Color.FromRgb(41, 41, 41));
                resources["ScrollBarThumbBackground"] = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                resources["CodeBackground"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                resources["AccentColor"] = new SolidColorBrush(Color.FromRgb(0, 137, 255));
            }
            
            // Force theme update on all elements
            foreach (Window window in Application.Current.Windows)
            {
                RefreshControlsRecursively(window);
            }
            
            // Notify subscribers
            ThemeChanged?.Invoke(this, theme);
            
            // Save setting
            Properties.Settings.Default.IsLightTheme = theme == ThemeType.Light;
            Properties.Settings.Default.Save();
            
            // Set system parameters
            System.Windows.SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
            System.Windows.SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }
        
        private void SystemParameters_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // If a system color or parameter changes, we might need to update our resources
            if (e.PropertyName == "MenuPopupAnimation" || e.PropertyName.StartsWith("MenuItem") || e.PropertyName.StartsWith("Menu"))
            {
                // Update menu-related resources
                var resources = Application.Current.Resources;
                if (CurrentTheme == ThemeType.Light)
                {
                    resources["MenuBackground"] = new SolidColorBrush(Colors.WhiteSmoke);
                }
                else
                {
                    resources["MenuBackground"] = new SolidColorBrush(Color.FromRgb(24, 24, 24));
                }
            }
        }
        
        private void RefreshControlsRecursively(DependencyObject parent)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                // Force the control to re-evaluate its templates and styles
                if (child is FrameworkElement element)
                {
                    element.InvalidateVisual();
                }
                
                // Recursively process all children
                RefreshControlsRecursively(child);
            }
        }
    }
}