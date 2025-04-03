using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        }
        
        /// <summary>
        /// Load tools from the registry
        /// </summary>
        private void LoadTools()
        {
            // Get all tools including disabled ones
            var allDefinitions = _toolRegistry.GetAllToolDefinitions();
            
            // Group by category
            var groupedTools = allDefinitions.GroupBy(t => t.Tags.FirstOrDefault() ?? "General");
            
            Categories.Clear();
            
            foreach (var group in groupedTools.OrderBy(g => g.Key))
            {
                var category = new ToolCategoryViewModel(group.Key);
                
                foreach (var tool in group.OrderBy(t => t.Name))
                {
                    category.Tools.Add(new ToolToggleViewModel(tool, _toolRegistry));
                }
                
                Categories.Add(category);
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
    }
}