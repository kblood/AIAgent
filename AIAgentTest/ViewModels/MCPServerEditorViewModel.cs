using System;
using System.Collections.Generic;
using System.Windows.Input;
using AIAgentTest.Commands;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for editing MCP servers
    /// </summary>
    public class MCPServerEditorViewModel : ViewModelBase
    {
        private string _name;
        private string _command;
        private string _argsString;
        private bool _isEnabled;
        private bool _isEditing;
        
        /// <summary>
        /// Server name
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    ValidateProperties();
                }
            }
        }
        
        /// <summary>
        /// Server command
        /// </summary>
        public string Command
        {
            get => _command;
            set
            {
                if (SetProperty(ref _command, value))
                {
                    ValidateProperties();
                }
            }
        }
        
        /// <summary>
        /// Arguments as a space-separated string
        /// </summary>
        public string ArgsString
        {
            get => _argsString;
            set
            {
                if (SetProperty(ref _argsString, value))
                {
                    ValidateProperties();
                }
            }
        }
        
        /// <summary>
        /// Get arguments as an array
        /// </summary>
        public string[] Args
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_argsString))
                    return new string[0];
                    
                return _argsString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }
        
        /// <summary>
        /// Server type (derived from command and args)
        /// </summary>
        public string Type
        {
            get
            {
                if (_command == "npx" && Args.Length > 0)
                {
                    if (Args[0].Contains("server-filesystem") || 
                        (Args.Length > 1 && Args[1].Contains("server-filesystem")))
                    {
                        return "filesystem";
                    }
                }
                return "custom";
            }
        }
        
        /// <summary>
        /// Whether the server is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
        
        /// <summary>
        /// Whether the server is being edited
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }
        
        /// <summary>
        /// Whether the input is valid
        /// </summary>
        public bool IsValid
        {
            get => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Command) && !string.IsNullOrWhiteSpace(ArgsString);
        }
        
        /// <summary>
        /// Available server presets
        /// </summary>
        public List<(string name, string command, string args)> ServerPresets { get; } = new List<(string, string, string)>
        {
            ("FileSystem", "npx", "-y @modelcontextprotocol/server-filesystem C:\\Users\\Documents")
        };
        
        /// <summary>
        /// Constructor for new server
        /// </summary>
        public MCPServerEditorViewModel()
        {
            var preset = ServerPresets[0];
            Command = preset.command;
            ArgsString = preset.args;
            IsEnabled = true;
            IsEditing = false;
        }
        
        /// <summary>
        /// Constructor for editing existing server
        /// </summary>
        /// <param name="server">Server to edit</param>
        public MCPServerEditorViewModel(MCPServerViewModel server)
        {
            if (server != null)
            {
                Name = server.Name;
                Command = server.Command;
                ArgsString = server.Args != null ? string.Join(" ", server.Args) : "";
                IsEnabled = server.IsEnabled;
                IsEditing = true;
            }
            else
            {
                var preset = ServerPresets[0];
                Command = preset.command;
                ArgsString = preset.args;
                IsEnabled = true;
                IsEditing = false;
            }
        }
        
        /// <summary>
        /// Validate properties
        /// </summary>
        private void ValidateProperties()
        {
            OnPropertyChanged(nameof(IsValid));
        }
    }
}
