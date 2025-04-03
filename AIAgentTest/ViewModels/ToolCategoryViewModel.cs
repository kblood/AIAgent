using System.Collections.ObjectModel;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel representing a category of tools
    /// </summary>
    public class ToolCategoryViewModel : ViewModelBase
    {
        private string _name;
        private ObservableCollection<ToolToggleViewModel> _tools;
        private bool _isExpanded;
        
        /// <summary>
        /// Name of the category
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        
        /// <summary>
        /// Tools in this category
        /// </summary>
        public ObservableCollection<ToolToggleViewModel> Tools
        {
            get => _tools;
            set => SetProperty(ref _tools, value);
        }
        
        /// <summary>
        /// Whether the category is expanded in the UI
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Name of the category</param>
        public ToolCategoryViewModel(string name)
        {
            Name = name;
            Tools = new ObservableCollection<ToolToggleViewModel>();
            IsExpanded = true; // Expanded by default
        }
    }
}