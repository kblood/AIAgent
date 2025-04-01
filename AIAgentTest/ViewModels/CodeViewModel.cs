using System;
using System.Windows.Input;
using AIAgentTest.Commands;
using System.IO;
using Microsoft.Win32;

namespace AIAgentTest.ViewModels
{
    public class CodeViewModel : ViewModelBase
    {
        private string _currentCode;
        private string _currentLanguage;
        
        public string CurrentCode
        {
            get => _currentCode;
            set => SetProperty(ref _currentCode, value);
        }
        
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set => SetProperty(ref _currentLanguage, value);
        }
        
        // Commands
        public ICommand ExportCodeCommand { get; }
        public ICommand CopyToClipboardCommand { get; }
        
        public CodeViewModel()
        {
            ExportCodeCommand = new RelayCommand(ExportCode, () => !string.IsNullOrEmpty(CurrentCode));
            CopyToClipboardCommand = new RelayCommand(CopyToClipboard, () => !string.IsNullOrEmpty(CurrentCode));
        }
        
        public void SetCode(string code, string language)
        {
            CurrentCode = code;
            CurrentLanguage = language;
        }
        
        private void ExportCode()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt"
            };
            
            // Add language-specific file extension if available
            if (!string.IsNullOrEmpty(CurrentLanguage))
            {
                switch (CurrentLanguage.ToLowerInvariant())
                {
                    case "csharp":
                    case "cs":
                        dialog.Filter = "C# files (*.cs)|*.cs|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                        dialog.DefaultExt = ".cs";
                        break;
                    case "python":
                    case "py":
                        dialog.Filter = "Python files (*.py)|*.py|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                        dialog.DefaultExt = ".py";
                        break;
                    case "javascript":
                    case "js":
                        dialog.Filter = "JavaScript files (*.js)|*.js|Text files (*.txt)|*.txt|All files (*.*)|*.*";
                        dialog.DefaultExt = ".js";
                        break;
                    // Add more language-specific extensions as needed
                }
            }
            
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, CurrentCode);
            }
        }
        
        private void CopyToClipboard()
        {
            System.Windows.Clipboard.SetText(CurrentCode);
        }
    }
}