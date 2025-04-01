# AIAgent MVVM Architecture Documentation

## Overview

This document provides an overview of the MVVM (Model-View-ViewModel) architecture implemented in the AIAgent project. The MVVM pattern improves code organization, testability, and maintainability by separating concerns into distinct layers.

## Architecture Components

### Models

Models represent the application data and business logic:

- `ChatSession`: Represents a conversation, including messages and metadata
- `ChatMessage`: Represents an individual message in a conversation

### ViewModels

ViewModels act as intermediaries between Models and Views:

- `ViewModelBase`: Base class for all ViewModels, implementing INotifyPropertyChanged
- `MainViewModel`: Coordinates the application and manages child ViewModels
- `ChatSessionViewModel`: Manages chat sessions and messaging
- `CodeViewModel`: Manages code display and export
- `DebugViewModel`: Manages debug information and context inspection
- `ModelSelectionViewModel`: Manages model selection and configuration

### Views

Views provide the user interface:

- `MainWindow`: Main application window containing all panels
- `ChatPanel`: Displays conversations and handles user input
- `CodePanel`: Displays extracted code blocks
- `DebugPanel`: Displays debug information and context
- `ModelSelectionPanel`: Provides UI for selecting AI models

### Services

Services implement business logic and API access:

- `LLMClientService`: Communicates with LLM providers (Ollama, LLamaSharp, etc.)
- `ChatSessionService`: Manages chat session persistence
- `ContextManager`: Handles conversation context and memory
- `MessageParsingService`: Parses responses to extract code and structure
- `ThemeService`: Manages application themes

## Dependency Injection

The application uses a simple service locator pattern implemented in `ServiceProvider.cs`. Services are registered at startup in `App.xaml.cs` and resolved when needed.

## Data Flow

1. **User Input**: Captured in the View and bound to ViewModel properties
2. **Command Execution**: ViewModel commands process the input
3. **Service Interaction**: ViewModels call services to perform actions
4. **Model Updates**: Services update models with results
5. **UI Updates**: Changes in ViewModels trigger property change notifications, updating the UI

## Event Communication

ViewModels communicate with each other through events:

- `CodeExtracted`: Triggered when code is extracted from a message
- `ModelsLoaded`: Triggered when available models are loaded

## Testing

The architecture includes built-in testing utilities:

- `TestUtil`: Provides methods to validate services, ViewModels, and data flow
- `MVVMTester`: Runs a suite of tests to verify the architecture is functioning correctly

Tests run automatically in debug mode to ensure the architecture remains functional during development.

## Theming

The application supports light and dark themes through the `ThemeService`. Theme changes are propagated through the `MainViewModel` and applied using application resources.

## Adding New Features

To add new features to the application:

1. **Add Models**: If needed, create new model classes in the Models directory
2. **Add Services**: Implement service interfaces in the Services directory
3. **Add ViewModels**: Create new ViewModels that inherit from ViewModelBase
4. **Add Views**: Create new XAML UserControls and code-behind
5. **Register Dependencies**: Add new services and ViewModels to the dependency injection in App.xaml.cs

## Best Practices

- Keep Views as thin as possible, moving logic to ViewModels and Services
- Use commands for user interactions rather than event handlers
- Ensure ViewModels don't reference Views directly
- Use interfaces for Services to facilitate testing and swapping implementations
- Maintain separation of concerns between layers

## Future Improvements

- Replace the simple service locator with a proper DI container
- Implement a messaging system for ViewModel-to-ViewModel communication
- Add unit tests for ViewModels and Services
- Implement a navigation service for multi-page applications
- Add logging and telemetry

## Conclusion

The MVVM architecture provides a solid foundation for the AIAgent application, improving maintainability, testability, and scalability. By following the established patterns, developers can extend the application with new features while maintaining clean separation of concerns.