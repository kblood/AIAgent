using System;
using System.Collections.ObjectModel;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for a category of tools
    /// </summary>
    public class ToolCategoryViewModel : ViewModelBase
    {
        private string _name;
        private ObservableCollection<ToolToggleViewModel> _tools;
        
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
        /// Constructor
        /// </summary>
        /// <param name="name">Category name</param>
        public ToolCategoryViewModel(string name)
        {
            Name = name;
            Tools = new ObservableCollection<ToolToggleViewModel>();
        }
    }
}
