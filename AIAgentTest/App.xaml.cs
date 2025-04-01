using System.Windows;
using AIAgentTest.API_Clients;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;
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
                
                var contextManager = new AbstractedContextManager(llmClient);
                ServiceProvider.RegisterService<IContextManager>(contextManager);
                
                var chatSessionService = new ChatSessionService();
                ServiceProvider.RegisterService<IChatSessionService>(chatSessionService);
                
                var messageParsingService = new MessageParsingService();
                ServiceProvider.RegisterService<IMessageParsingService>(messageParsingService);
                
                var themeService = new ThemeService();
                ServiceProvider.RegisterService<ThemeService>(themeService);
                
                // Register ViewModels
                var codeViewModel = new CodeViewModel();
                ServiceProvider.RegisterService<CodeViewModel>(codeViewModel);
                
                var debugViewModel = new DebugViewModel(contextManager);
                ServiceProvider.RegisterService<DebugViewModel>(debugViewModel);
                
                var modelSelectionViewModel = new ModelSelectionViewModel(llmClientService);
                ServiceProvider.RegisterService<ModelSelectionViewModel>(modelSelectionViewModel);
                
                var chatSessionViewModel = new ChatSessionViewModel(
                    llmClientService,
                    chatSessionService,
                    contextManager,
                    messageParsingService);
                ServiceProvider.RegisterService<ChatSessionViewModel>(chatSessionViewModel);
                
                var mainViewModel = new MainViewModel(
                    themeService,
                    modelSelectionViewModel,
                    codeViewModel,
                    debugViewModel,
                    chatSessionViewModel);
                ServiceProvider.RegisterService<MainViewModel>(mainViewModel);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing services: {ex.Message}");
                // Don't crash the app if our services have issues
            }
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