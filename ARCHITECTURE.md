# AI Agent Application Architecture

## Overview

The AI Agent is a .NET application built using the MVVM (Model-View-ViewModel) architectural pattern. This design separates the user interface, business logic, and data management into distinct layers, improving maintainability, scalability, and testability.

---

## Solution Structure

- **AIAgentTest.sln**: The Visual Studio solution file that organizes the project.
- **AIAgentTest/**: The main project directory containing all source code, organized into feature-based and layer-based folders.
- **.vscode/**: Editor configuration files.
- **.roo/**: Roo AI agent configuration files.
- **README.md**: General project overview.
- **ARCHITECTURE.md**: This documentation file.

---

## Project Organization (`AIAgentTest/`)

### Models
Represent the core data structures and business entities:
- `ChatSession`: Represents a conversation, including messages and metadata.
- `ChatMessage`: Represents an individual message in a conversation.

### ViewModels
Act as intermediaries between the UI and business logic, exposing data and commands:
- `ViewModelBase`: Base class implementing property change notifications.
- `MainViewModel`: Coordinates the overall application and manages child ViewModels.
- `ChatSessionViewModel`: Manages chat sessions and messaging.
- `CodeViewModel`: Handles code display and export.
- `DebugViewModel`: Manages debug information and context inspection.
- `ModelSelectionViewModel`: Handles AI model selection and configuration.

### Views
User interface components built with XAML:
- `MainWindow`: The main application window hosting all panels.
- `ChatPanel`: Displays conversations and user input.
- `CodePanel`: Shows extracted code blocks.
- `DebugPanel`: Displays debugging information.
- `ModelSelectionPanel`: UI for selecting AI models.

### Services
Provide business logic, API communication, and utility functions:
- `LLMClientService`: Interfaces with AI model providers (e.g., Ollama, LLamaSharp).
- `ChatSessionService`: Manages chat session persistence.
- `ContextManager`: Handles conversation context and memory.
- `MessageParsingService`: Parses AI responses to extract code and structure.
- `ThemeService`: Manages application themes.

### API_Clients
Contains client classes for interacting with external APIs.

### Commands
Implements command patterns for user actions, bound to UI elements.

### Converters
Includes data converters for UI bindings, such as:
- Null-to-Visibility
- Boolean-to-Visibility
- Boolean-to-String

### Testing and Tests
Contains automated tests and testing utilities to validate functionality.

---

## Important Files

- **App.xaml**: Defines application-wide resources, themes, and startup configuration.
- **App.xaml.cs**: Handles application lifecycle events, initializes services, and sets up dependency injection.
- **Program.cs**: Contains the main entry point, utility methods for chat and image processing, and test routines.
- **ServiceProvider.cs**: Implements a simple service locator pattern for dependency injection.
- **README_MVVM.md**: Detailed explanation of the MVVM architecture and development guidelines.

---

## Dependency Injection

The app uses a basic service locator pattern:
- Services are registered during startup in `App.xaml.cs`.
- ViewModels and other components resolve dependencies via `ServiceProvider.cs`.
- This approach facilitates modularity and testing.

---

## Data Flow

1. **User Input** is captured in Views and bound to ViewModel properties.
2. **Commands** in ViewModels process user actions.
3. **Services** perform business logic and API calls.
4. **Models** are updated with results.
5. **ViewModels** notify Views of changes, updating the UI dynamically.

---

## Event Communication

ViewModels communicate via events, such as:
- `CodeExtracted`: When code is parsed from AI responses.
- `ModelsLoaded`: When available AI models are loaded.

---

## Theming

Supports light and dark themes managed by `ThemeService`. Theme changes propagate through `MainViewModel` and are applied via application resources.

---

## Testing

Includes utilities and automated tests to validate:
- Services
- ViewModels
- Data flow

Tests run automatically in debug mode to ensure stability during development.

---

## Extending the Application

To add new features:
1. Create new **Models** if needed.
2. Implement **Services** for business logic.
3. Develop **ViewModels** inheriting from `ViewModelBase`.
4. Build **Views** with XAML and bind to ViewModels.
5. Register new components in the dependency injection setup.

---

## Summary

The AI Agent app is a modular, MVVM-structured .NET application designed for maintainability and extensibility. It cleanly separates UI, business logic, and data, enabling efficient development and testing of AI-powered features.