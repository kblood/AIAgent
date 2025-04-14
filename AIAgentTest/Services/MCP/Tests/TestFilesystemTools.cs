using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.Services.MCP.Tests
{
    /// <summary>
    /// Simple test utility to check if the filesystem tools are working
    /// </summary>
    public class TestFilesystemTools
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly CommonTools _commonTools;
        
        public TestFilesystemTools()
        {
            _toolRegistry = new ToolRegistry();
            _commonTools = new CommonTools();
            
            // Configure allowed directories
            string testDir = Path.Combine(Path.GetTempPath(), "AIAgent_TestDir");
            if (!Directory.Exists(testDir))
            {
                Directory.CreateDirectory(testDir);
            }
            _commonTools.AddAllowedDirectory(testDir);
            
            // Register tools
            _commonTools.RegisterCommonTools(_toolRegistry);
            
            Console.WriteLine($"Test directory created at: {testDir}");
        }
        
        /// <summary>
        /// Run a simple test to verify that the filesystem tools are working
        /// </summary>
        public async Task RunTest()
        {
            // Get the test directory
            string testDir = Path.Combine(Path.GetTempPath(), "AIAgent_TestDir");
            
            Console.WriteLine("Testing filesystem tools...");
            Console.WriteLine();
            
            // Test 1: List allowed directories
            Console.WriteLine("Test 1: List allowed directories");
            var listAllowedDirsHandler = _toolRegistry.GetToolHandler("list_allowed_directories");
            var allowedDirsResult = await listAllowedDirsHandler(new { });
            Console.WriteLine(FormatJson(allowedDirsResult));
            Console.WriteLine();
            
            // Test 2: Write a test file
            Console.WriteLine("Test 2: Write a test file");
            var writeFileHandler = _toolRegistry.GetToolHandler("write_file");
            string testFilePath = Path.Combine(testDir, "test.txt");
            var writeResult = await writeFileHandler(new { path = testFilePath, content = "This is a test file." });
            Console.WriteLine(FormatJson(writeResult));
            Console.WriteLine();
            
            // Test 3: Read the test file
            Console.WriteLine("Test 3: Read the test file");
            var readFileHandler = _toolRegistry.GetToolHandler("read_file");
            var readResult = await readFileHandler(new { path = testFilePath });
            Console.WriteLine(FormatJson(readResult));
            Console.WriteLine();
            
            // Test 4: List directory
            Console.WriteLine("Test 4: List directory");
            var listDirHandler = _toolRegistry.GetToolHandler("list_directory");
            var listDirResult = await listDirHandler(new { path = testDir });
            Console.WriteLine(FormatJson(listDirResult));
            Console.WriteLine();
            
            // Test 5: Edit file
            Console.WriteLine("Test 5: Edit file");
            var editFileHandler = _toolRegistry.GetToolHandler("edit_file");
            var editFileResult = await editFileHandler(new
            {
                path = testFilePath,
                edits = new[]
                {
                    new { oldText = "This is a test file.", newText = "This is an edited test file." }
                },
                dryRun = false
            });
            Console.WriteLine(FormatJson(editFileResult));
            Console.WriteLine();
            
            // Test 6: Create directory
            Console.WriteLine("Test 6: Create directory");
            var createDirHandler = _toolRegistry.GetToolHandler("create_directory");
            string nestedDirPath = Path.Combine(testDir, "nested");
            var createDirResult = await createDirHandler(new { path = nestedDirPath });
            Console.WriteLine(FormatJson(createDirResult));
            Console.WriteLine();
            
            // Test 7: Move file
            Console.WriteLine("Test 7: Move file");
            var moveFileHandler = _toolRegistry.GetToolHandler("move_file");
            string movedFilePath = Path.Combine(nestedDirPath, "moved.txt");
            var moveFileResult = await moveFileHandler(new { source = testFilePath, destination = movedFilePath });
            Console.WriteLine(FormatJson(moveFileResult));
            Console.WriteLine();
            
            // Test 8: Get directory tree
            Console.WriteLine("Test 8: Get directory tree");
            var dirTreeHandler = _toolRegistry.GetToolHandler("directory_tree");
            var dirTreeResult = await dirTreeHandler(new { path = testDir });
            Console.WriteLine(FormatJson(dirTreeResult));
            Console.WriteLine();
            
            // Test 9: Get file info
            Console.WriteLine("Test 9: Get file info");
            var fileInfoHandler = _toolRegistry.GetToolHandler("get_file_info");
            var fileInfoResult = await fileInfoHandler(new { path = movedFilePath });
            Console.WriteLine(FormatJson(fileInfoResult));
            Console.WriteLine();
            
            // Test 10: Search files
            Console.WriteLine("Test 10: Search files");
            // Create some more files for testing search
            for (int i = 1; i <= 3; i++)
            {
                string filePath = Path.Combine(testDir, $"file{i}.txt");
                await writeFileHandler(new { path = filePath, content = $"This is file {i}" });
            }
            
            var searchHandler = _toolRegistry.GetToolHandler("search_files");
            var searchResult = await searchHandler(new { path = testDir, pattern = "*.txt" });
            Console.WriteLine(FormatJson(searchResult));
            Console.WriteLine();
            
            // Test 11: Read multiple files
            Console.WriteLine("Test 11: Read multiple files");
            var readMultipleHandler = _toolRegistry.GetToolHandler("read_multiple_files");
            var filePaths = new[]
            {
                Path.Combine(testDir, "file1.txt"),
                Path.Combine(testDir, "file2.txt"),
                Path.Combine(testDir, "file3.txt")
            };
            var readMultipleResult = await readMultipleHandler(new { paths = filePaths });
            Console.WriteLine(FormatJson(readMultipleResult));
            Console.WriteLine();
            
            Console.WriteLine("All tests completed!");
        }
        
        /// <summary>
        /// Format an object as pretty JSON
        /// </summary>
        private string FormatJson(object obj)
        {
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }
        
        /// <summary>
        /// Execute the test
        /// </summary>
        public static async Task ExecuteTest()
        {
            var test = new TestFilesystemTools();
            await test.RunTest();
            
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}