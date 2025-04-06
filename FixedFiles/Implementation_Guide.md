# MCP Server Fixes Implementation Guide

This document contains the implementation details and steps to fix the issues with MCP server configuration and execution in the AIAgent project.

## Issue Summary:

1. **Process Execution Issue**: The `StdioMCPServerClient` fails to start NPX because it's using an invalid working directory (`C:/`).
2. **--stdio Flag Duplication**: The `--stdio` flag is being duplicated in server configurations.
3. **Settings Not Being Saved**: Server settings are not being properly saved between sessions.

## Files Modified:

### 1. StdioMCPServerClient.cs

Key changes:
- Modified the `StartServerAsync()` method to use a reliable system directory instead of `C:/`
- Added more robust error handling and logging
- Changed to use "cmd.exe" with "/c" to help find the npx command

### 2. MCPServerManagerViewModel.cs

Key changes:
- Fixed the `AddServer()` method to properly handle the `--stdio` flag
- Fixed the `EditServer()` method to properly handle the `--stdio` flag
- Fixed the `SaveServersToConfiguration()` method to avoid duplicate flags
- Added extensive debug logging throughout

### 3. MCPServerRegistration.cs

Key changes:
- Fixed the `RegisterServerFromConfig()` method to properly process arguments
- Added better working directory handling
- Fixed flag duplication issues

## Implementation Steps:

1. **Replace StdioMCPServerClient.cs**:
   - Open `AIAgent\AIAgentTest\API_Clients\MCP\StdioMCPServerClient.cs`
   - Replace with the contents of `StdioMCPServerClient_Fix.cs`

2. **Update MCPServerManagerViewModel.cs**:
   - Open `AIAgent\AIAgentTest\ViewModels\MCPServerManagerViewModel.cs`
   - Replace the following methods with their fixed versions:
     - `AddServer()`
     - `EditServer()`
     - `SaveServersToConfiguration()`

3. **Update MCPServerRegistration.cs**:
   - Open `AIAgent\AIAgentTest\Services\MCP\MCPServerRegistration.cs`
   - Replace the `RegisterServerFromConfig()` method with the fixed version

## Key Changes Explanation:

### Process Execution:
- Changed working directory from `C:/` to a reliable system directory
- Added more robust error logging
- Used "cmd.exe" with "/c" to help find npx command

### --stdio Flag Handling:
- Added logic to check if the flag is already present before adding it
- Added code to insert the flag in the correct position (after package name)
- Removed hardcoded arguments in configuration saving

### Settings Persistence:
- Added more verbose logging to identify saving issues
- Added fallback paths if primary save location fails
- Ensured consistent argument handling when saving settings

## Testing:

After implementing these changes:

1. Start the application
2. Go to the MCP Servers tab
3. Try adding a new server
4. Verify no duplicate `--stdio` flags appear in the configuration
5. Restart the application and verify the server remains correctly configured

If the server still fails to start, check the logs for:
- "Error starting server"
- "Den angivne fil blev ikke fundet" (File not found error)

You may need to ensure npx is installed and accessible from the command line.
