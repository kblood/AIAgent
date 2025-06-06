using System;
using System.Threading.Tasks;
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
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            MainViewModel mainViewModel = null;
            Views.TestWindow mainWindow = null;

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
                
                // Create debug view model early so we can use it for logging
                var debugViewModel = new DebugViewModel(null); // Temporarily null, will update later
                ServiceProvider.RegisterService<DebugViewModel>(debugViewModel);
                
                // Register MCP services and properly await
                await AIAgentTest.Services.MCP.MCPServiceRegistration.RegisterMCPServicesAsync();
                
                var themeService = new ThemeService();
                ServiceProvider.RegisterService<ThemeService>(themeService);
                
                var modelSelectionViewModel = new ModelSelectionViewModel(llmClientService);
                ServiceProvider.RegisterService<ModelSelectionViewModel>(modelSelectionViewModel);
                
                // Create MCP-enabled ViewModels
                var mcpLLMClientService = ServiceProvider.GetService<IMCPLLMClientService>();
                var mcpContextManager = ServiceProvider.GetService<IMCPContextManager>();
                var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
                
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
                
                // DebugViewModel was already created
                
                // Check if MCPClientFactory service is available
                // mcpClientFactory was already declared above
                if (mcpClientFactory != null)
                {
                    System.Diagnostics.Debug.WriteLine("MCPClientFactory service is available.");
                    var servers = mcpClientFactory.GetAllRegisteredServers();
                    System.Diagnostics.Debug.WriteLine($"MCPClientFactory has {servers.Count} registered servers: {string.Join(", ", servers)}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: MCPClientFactory service is NOT available.");
                }

                // Check if ToolRegistry service is available
                var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
                if (toolRegistry != null)
                {
                    System.Diagnostics.Debug.WriteLine("ToolRegistry service is available.");
                    var tools = toolRegistry.GetAllTools();
                    System.Diagnostics.Debug.WriteLine($"ToolRegistry has {tools.Count} registered tools.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: ToolRegistry service is NOT available.");
                }

                // Create MCP tool manager ViewModels
                var toolManagerViewModel = new ToolManagerViewModel(
                    ServiceProvider.GetService<IToolRegistry>());
                ServiceProvider.RegisterService<ToolManagerViewModel>(toolManagerViewModel);

                // Add server manually first for UI display
                var mcpServerManagerViewModel = new MCPServerManagerViewModel(
                    ServiceProvider.GetService<MCPClientFactory>());
                
                // Create a fake server to ensure we always see the server in UI
                if (mcpServerManagerViewModel.Servers.Count == 0) {
                    var debugLogger = ServiceProvider.GetService<IDebugLogger>();
                    debugLogger?.Log("Adding FileServer to UI manually");
                    
                    mcpServerManagerViewModel.Servers.Add(new MCPServerViewModel {
                        Name = "FileServer",
                        Command = "npx",
                        Args = new[] { "-y", "@modelcontextprotocol/server-filesystem", "C:/" },
                        IsEnabled = true
                    });
                }
                
                ServiceProvider.RegisterService<MCPServerManagerViewModel>(mcpServerManagerViewModel);
                
                // Create settings ViewModel
                var settingsViewModel = new SettingsViewModel(
                    llmClientService,
                    mcpContextManager);
                ServiceProvider.RegisterService<SettingsViewModel>(settingsViewModel);
                
                // Create filesystem manager ViewModel
                var commonTools = ServiceProvider.GetService<CommonTools>();
                var filesystemManagerViewModel = new FilesystemManagerViewModel(
                    commonTools,
                    debugViewModel);
                ServiceProvider.RegisterService<FilesystemManagerViewModel>(filesystemManagerViewModel);
                
                // Create tool testing ViewModel using the already retrieved IToolRegistry instance
                var toolTestingViewModel = new ToolTestingViewModel(
                    ServiceProvider.GetService<IToolRegistry>(),
                    debugViewModel);
                ServiceProvider.RegisterService<ToolTestingViewModel>(toolTestingViewModel);
                
                // Create main view model with MCP components
                mainViewModel = new MainViewModel(
                    themeService,
                    modelSelectionViewModel,
                    codeViewModel,
                    debugViewModel,
                    chatSessionViewModel,
                    toolManagerViewModel,
                    mcpServerManagerViewModel,
                    settingsViewModel,
                    filesystemManagerViewModel,
                    toolTestingViewModel);
                ServiceProvider.RegisterService<MainViewModel>(mainViewModel);
                
                // Now that all services are registered, create and show the main window
                mainWindow = new Views.TestWindow();
                Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing services: {ex.Message}");
                // Don't crash the app if our services have issues
            }
            
            // The window initializes its own DataContext
            
#if DEBUG
            // Run MVVM architecture tests in debug mode
            Testing.MVVMTester.RunTests().ConfigureAwait(false);
            
            // Run MCP tool call parsing tests
            Tests.MCPToolCallParsingTest.ValidateToolCallParsing().ConfigureAwait(false);
#endif
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Clean up MCP servers first
                var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
                if (mcpClientFactory != null)
                {
                    System.Diagnostics.Debug.WriteLine("Shutting down MCP servers...");
                    
                    // Get all server names
                    var serverNames = mcpClientFactory.GetRegisteredServerNames();
                    
                    // Create a background task for each server
                    foreach (var serverName in serverNames)
                    {
                        System.Diagnostics.Debug.WriteLine($"Initiating shutdown for MCP server '{serverName}'...");
                        
                        // Don't wait for completion - we can't block on exit
                        Task.Run(() => {
                            try
                            {
                                // Remove the server - this will also stop it
                                mcpClientFactory.RemoveMCPServerAsync(serverName);
                                System.Diagnostics.Debug.WriteLine($"Successfully initiated shutdown for MCP server '{serverName}'");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error shutting down MCP server '{serverName}': {ex.Message}");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during MCP cleanup: {ex.Message}");
            }
            
            // Clean up services
            LLMClientFactory.ReleaseAllClients();
            ServiceProvider.ClearServices();
            
            base.OnExit(e);
        }
    }
}