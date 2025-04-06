using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services.MCP;
using System.Collections.Generic;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for managing tools
    /// </summary>
    public class ToolManagerViewModel : ViewModelBase
    {
        private readonly IToolRegistry _toolRegistry;
        private ObservableCollection<ToolCategoryViewModel> _categories;
        private string _searchText;
        
        /// <summary>
        /// Command to enable all tools
        /// </summary>
        public ICommand EnableAllCommand { get; }
        
        /// <summary>
        /// Categories of tools
        /// </summary>
        public ObservableCollection<ToolCategoryViewModel> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }
        
        /// <summary>
        /// Text to search for tools
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterTools();
                }
            }
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="toolRegistry">Tool registry</param>
        public ToolManagerViewModel(IToolRegistry toolRegistry)
        {
            _toolRegistry = toolRegistry;
            Categories = new ObservableCollection<ToolCategoryViewModel>();
            
            // Load tools and organize by category
            LoadTools();
            
            // Subscribe to tool registry changes
            toolRegistry.ToolsChanged += (s, e) => LoadTools();
            
            // Initialize commands
            EnableAllCommand = new RelayCommand(EnableAllTools);
        }
        
        /// <summary>
        /// Load tools from the registry
        /// </summary>
        private void LoadTools()
        {
            if (_toolRegistry == null)
            {
                Console.WriteLine("ToolRegistry is null in ToolManagerViewModel");
                return;
            }
            
            try
            {
                // Get all tools including disabled ones
                var allDefinitions = _toolRegistry.GetAllToolDefinitions();
                Console.WriteLine($"Found {allDefinitions.Count} tool definitions in registry");
                
                // Group by category
                var groupedTools = allDefinitions.GroupBy(t => t.Tags?.FirstOrDefault() ?? "General");
                
                Categories.Clear();
                
                foreach (var group in groupedTools.OrderBy(g => g.Key))
                {
                    var category = new ToolCategoryViewModel(group.Key);
                    Console.WriteLine($"Creating category: {group.Key} with {group.Count()} tools");
                    
                    foreach (var tool in group.OrderBy(t => t.Name))
                    {
                        category.Tools.Add(new ToolToggleViewModel(tool, _toolRegistry));
                        Console.WriteLine($"Added tool: {tool.Name} to category {group.Key}");
                    }
                    
                    Categories.Add(category);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading tools: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Filter tools based on search text
        /// </summary>
        private void FilterTools()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // Show all tools
                LoadTools();
                return;
            }
            
            // Get all tools including disabled ones
            var allDefinitions = _toolRegistry.GetAllToolDefinitions();
            
            // Filter tools by search text
            var filteredTools = allDefinitions
                .Where(t => 
                    t.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Name);
                
            Categories.Clear();
            
            // Create a single "Search Results" category
            var searchCategory = new ToolCategoryViewModel("Search Results");
            
            foreach (var tool in filteredTools)
            {
                searchCategory.Tools.Add(new ToolToggleViewModel(tool, _toolRegistry));
            }
            
            Categories.Add(searchCategory);
        }
        
        /// <summary>
        /// Enable all tools
        /// </summary>
        private void EnableAllTools()
        {
            // Get all tools
            var tools = _toolRegistry.GetAllToolDefinitions();
            
            // Enable all tools
            foreach (var tool in tools)
            {
                _toolRegistry.EnableTool(tool.Name);
            }
            
            // Reload tools
            LoadTools();
        }
    }
}