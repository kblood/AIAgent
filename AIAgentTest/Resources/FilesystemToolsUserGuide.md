# Filesystem Tools User Guide

This guide explains how to use the built-in filesystem tools in AIAgent.

## Overview

AIAgent includes various filesystem tools that allow AI models to read, write, and manipulate files on your system. These tools follow the Model Context Protocol (MCP) standard, which enables AI models to work with external tools and APIs.

## Security Considerations

By default, filesystem tools can only access specific directories on your system. You can manage these allowed directories in the **Filesystem Manager** tab.

**Important:** Be careful when adding directories to the allowed list, especially system directories or directories containing sensitive information.

## Available Tools

The following filesystem tools are available:

### 1. `read_file`
- **Description:** Reads the content of a file
- **Parameters:**
  - `path`: Path to the file
- **Example:** `{"path": "C:\\example.txt"}`

### 2. `read_multiple_files`
- **Description:** Reads multiple files at once
- **Parameters:**
  - `paths`: Array of file paths
- **Example:** `{"paths": ["C:\\file1.txt", "C:\\file2.txt"]}`

### 3. `write_file`
- **Description:** Creates or overwrites a file
- **Parameters:**
  - `path`: Path to the file
  - `content`: Content to write
- **Example:** `{"path": "C:\\example.txt", "content": "Hello, world!"}`

### 4. `edit_file`
- **Description:** Makes line-based edits to a file
- **Parameters:**
  - `path`: Path to the file
  - `edits`: Array of edit operations
  - `dryRun`: (Optional) Preview changes without saving
- **Example:**
```json
{
  "path": "C:\\example.txt",
  "edits": [
    {
      "oldText": "Hello, world!",
      "newText": "Hello, universe!"
    }
  ],
  "dryRun": false
}
```

### 5. `create_directory`
- **Description:** Creates a directory (and parent directories if needed)
- **Parameters:**
  - `path`: Path to create
- **Example:** `{"path": "C:\\MyFolder\\SubFolder"}`

### 6. `list_directory`
- **Description:** Lists files and directories in a path
- **Parameters:**
  - `path`: Directory to list
- **Example:** `{"path": "C:\\MyFolder"}`

### 7. `directory_tree`
- **Description:** Gets a recursive tree view of files and directories
- **Parameters:**
  - `path`: Root directory
- **Example:** `{"path": "C:\\MyFolder"}`

### 8. `move_file`
- **Description:** Moves or renames files and directories
- **Parameters:**
  - `source`: Source path
  - `destination`: Destination path
- **Example:** `{"source": "C:\\oldname.txt", "destination": "C:\\newname.txt"}`

### 9. `search_files`
- **Description:** Searches for files matching a pattern
- **Parameters:**
  - `path`: Directory to search in
  - `pattern`: Search pattern (e.g., "*.txt")
  - `excludePatterns`: (Optional) Patterns to exclude
- **Example:** `{"path": "C:\\MyFolder", "pattern": "*.txt"}`

### 10. `get_file_info`
- **Description:** Gets metadata about a file or directory
- **Parameters:**
  - `path`: Path to file or directory
- **Example:** `{"path": "C:\\example.txt"}`

### 11. `list_allowed_directories`
- **Description:** Lists directories available for filesystem operations
- **Parameters:** None
- **Example:** `{}`

## How to Test Tools

You can test these tools directly from the **Tool Testing** tab:

1. Select a tool from the list
2. Enter the input parameters as JSON
3. Click "Execute Tool" to see the result

## Common Use Cases

- **Reading configuration files:** Use `read_file` to load configuration or data files
- **Saving results:** Use `write_file` to save analysis results or generated content
- **Finding files:** Use `search_files` to locate files matching specific patterns
- **Project setup:** Use `create_directory` and other tools to set up project structures

## Troubleshooting

If you encounter errors:

1. Check that the path is allowed in the Filesystem Manager
2. Verify that input parameters match the expected format
3. Make sure file paths are valid and accessible
4. Look for detailed error messages in the tool result

## Further Help

For more information about the Model Context Protocol and tool usage, please refer to the MCP documentation. If you have specific questions about using filesystem tools, you can ask the AI model directly.
