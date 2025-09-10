namespace McpCodeEditor.Services.Advanced
{
    public interface IAdvancedFileReaderService
    {
        // Basic Line Range Reading
        Task<FileReadResult> ReadRangeAsync(string filePath, int startLine, int endLine);
        Task<FileReadResult> ReadAroundLineAsync(string filePath, int lineNumber, int contextLines = 10);
        
        // Code-Aware Reading (Roslyn-powered)
        Task<FileReadResult> ReadMethodAsync(string filePath, string methodName, int contextLines = 5);
        Task<FileReadResult> ReadClassAsync(string filePath, string className);
        
        // Structural Analysis
        Task<FileStructureResult> GetFileOutlineAsync(string filePath);
        Task<FileReadResult> ReadMethodSignaturesAsync(string filePath);
        Task<FileReadResult> ReadImportsAndHeaderAsync(string filePath);
        
        // Search-Based Reading
        Task<FileReadResult> ReadSearchAsync(string filePath, string pattern, int contextLines = 3, bool useRegex = false);
        
        // Incremental Reading
        Task<FileReadResult> ReadNextChunkAsync(string filePath, int startLine, int maxLines = 100);
    }

    public class FileReadResult
    {
        public bool Success { get; set; }
        public string Content { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public int TotalLines { get; set; }
        public string ReadMethod { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = [];
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        public static FileReadResult Error(string message) => new() 
        { 
            Success = false, 
            Warnings = [message]
        };
    }
    
    public class FileStructureResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public List<ClassInfo> Classes { get; set; } = [];
        public List<MethodInfo> Methods { get; set; } = [];
        public List<string> UsingStatements { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
    }
    
    public class ClassInfo
    {
        public string Name { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string AccessModifier { get; set; } = string.Empty;
        public List<MethodInfo> Methods { get; set; } = [];
    }
    
    public class MethodInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public string AccessModifier { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public bool IsAsync { get; set; }
        public int ComplexityScore { get; set; }
        public string Signature { get; set; } = string.Empty;
    }
}
