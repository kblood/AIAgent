# AIAgent Framework

An experimental AI agent framework built in C#/.NET 8, designed to provide flexible interfaces for working with multiple LLM providers.

## Overview

This project is an experimental AI agent framework that allows interaction with various Large Language Models through a unified interface. The framework is built in C# as it's the language I'm most familiar with.

## Architecture

The project has been refactored to follow the MVVM (Model-View-ViewModel) architecture pattern for improved maintainability, testability, and separation of concerns. The architecture consists of:

- **Models**: Data structures representing chat sessions, messages, etc.
- **Views**: WPF user interfaces
- **ViewModels**: Intermediate layer connecting models to views
- **Services**: Business logic and API interfaces

For more details on the MVVM implementation, see [README_MVVM.md](AIAgentTest/README_MVVM.md).

## Features

- Support for multiple LLM providers (Ollama, LLamaSharp, LM Studio, OpenAI, etc.)
- Rich conversation UI with support for images and code blocks
- Context management with automatic summarization
- Code extraction and display
- Theme support (light and dark modes)
- Session management for saving and loading conversations

## Requirements

- .NET 8.0
- Windows (WPF)
- GPU with CUDA 12 support (for local inference)

## Dependencies

- LangChain and LangChain.Core
- LLamaSharp
- Selenium WebDriver
- HtmlAgilityPack
- SixLabors.ImageSharp
- System.Text.Json

## Getting Started

1. Clone the repository
2. Open the solution in Visual Studio 2022
3. Restore NuGet packages
4. Build and run the application

## License

This project is licensed under the MIT License - see the LICENSE.txt file for details.

## Future Improvements

- Additional LLM provider integrations
- Cross-platform UI using .NET MAUI
- Enhanced tool capabilities and function calling
- Improved document processing and embedding
- Agent workflow automation
