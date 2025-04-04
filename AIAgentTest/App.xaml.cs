using System.Windows;
using AIAgentTest.API_Clients;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;
using AIAgentTest.ViewModels;

namespace AIAgentTest
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize services for our new MVVM architecture
            try
            {
                // Get client from factory
                var llmClient = LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
                
                // Initialize and register services
                var llmClientService = new LLMClientService(llmClient);
                ServiceProvider.RegisterService<ILLMClientService>(llmClientService);
                
                var contextManager = new AbstractedContextManager(llmClient, "llama3");
                ServiceProvider.RegisterService<IContextManager>(contextManager);
                
                var chatSessionService = new ChatSessionService();
                ServiceProvider.RegisterService<IChatSessionService>(chatSessionService);
                
                var messageParsingService = new MessageParsingService();
                ServiceProvider.RegisterService<IMessageParsingService>(messageParsingService);
                
                // Register MCP services
                AIAgentTest.Services.MCP.MCPServiceRegistration.RegisterMCPServices();
                
                var themeService = new ThemeService();
                ServiceProvider.RegisterService<ThemeService>(themeService);
                
                var modelSelectionViewModel = new ModelSelectionViewModel(llmClientService);
                ServiceProvider.RegisterService<ModelSelectionViewModel>(modelSelectionViewModel);
                
                // Create MCP-enabled ViewModels
                var mcpLLMClientService = ServiceProvider.GetService<IMCPLLMClientService>();
                var mcpContextManager = ServiceProvider.GetService<IMCPContextManager>();
                
                // Use MCP-aware chat session view model with MCP services
                var chatSessionViewModel = new ChatSessionViewModel(
                    mcpLLMClientService, // Use MCP-enabled service
                    chatSessionService,
                    mcpContextManager, // Use MCP context manager
                    messageParsingService);
                ServiceProvider.RegisterService<ChatSessionViewModel>(chatSessionViewModel);
                
                // Set initial model in ChatSessionViewModel
                if (!string.IsNullOrEmpty(modelSelectionViewModel.SelectedModel))
                {
                    chatSessionViewModel.SelectedModel = modelSelectionViewModel.SelectedModel;
                }
                
                // Register property change event for model selection
                modelSelectionViewModel.PropertyChanged += (s, e) => {
                    if (e.PropertyName == nameof(ModelSelectionViewModel.SelectedModel)) {
                        chatSessionViewModel.SelectedModel = modelSelectionViewModel.SelectedModel;
                    }
                };
                
                var codeViewModel = new CodeViewModel();
                ServiceProvider.RegisterService<CodeViewModel>(codeViewModel);
                
                var debugViewModel = new DebugViewModel(mcpContextManager);
                ServiceProvider.RegisterService<DebugViewModel>(debugViewModel);
                
                // Create MCP tool manager ViewModels
                var toolManagerViewModel = new ToolManagerViewModel(
                    ServiceProvider.GetService<IToolRegistry>());
                ServiceProvider.RegisterService<ToolManagerViewModel>(toolManagerViewModel);

                var mcpServerManagerViewModel = new MCPServerManagerViewModel(
                    ServiceProvider.GetService<MCPClientFactory>());
                ServiceProvider.RegisterService<MCPServerManagerViewModel>(mcpServerManagerViewModel);
                
                // Create main view model with MCP components
                var mainViewModel = new MainViewModel(
                    themeService,
                    modelSelectionViewModel,
                    codeViewModel,
                    debugViewModel,
                    chatSessionViewModel,
                    toolManagerViewModel,
                    mcpServerManagerViewModel);
                ServiceProvider.RegisterService<MainViewModel>(mainViewModel);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing services: {ex.Message}");
                // Don't crash the app if our services have issues
            }
            
            // Create and show the main window
            var mainWindow = new Views.TestWindow();
            mainWindow.DataContext = ServiceProvider.GetService<MainViewModel>();
            mainWindow.Show();
            Current.MainWindow = mainWindow;
            
#if DEBUG
            // Run MVVM architecture tests in debug mode
            Testing.MVVMTester.RunTests().ConfigureAwait(false);
#endif
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up services
            LLMClientFactory.ReleaseAllClients();
            ServiceProvider.ClearServices();
            
            base.OnExit(e);
        }
    }
}