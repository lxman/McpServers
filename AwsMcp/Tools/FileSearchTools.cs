using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace AwsMcp.Tools;

[McpServerToolType]
public class FileSearchTools
{
    [McpServerTool]
    [Description("Search large files using regex patterns without loading entire file into memory")]
    public async Task<string> SearchFileWithRegexAsync(
        [Description("Path to the file to search")] string filePath,
        [Description("Regex pattern to search for")] string regexPattern,
        [Description("Number of context lines around matches (default: 3)")] int contextLines = 3,
        [Description("Case sensitive search (default: false)")] bool caseSensitive = false,
        [Description("Maximum matches to return (default: 50)")] int maxMatches = 50,
        [Description("Skip first N lines (useful for large logs, default: 0)")] int skipLines = 0)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "File not found",
                    filePath
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            
            var regex = new Regex(regexPattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            var matches = new List<object>();
            var lines = new List<string>();
            var lineNumber = 0;
            
            using var reader = new StreamReader(filePath);
            string? line;
            
            // Skip initial lines if requested
            for (int i = 0; i < skipLines && !reader.EndOfStream; i++)
            {
                reader.ReadLine();
                lineNumber++;
            }
            
            // Read file line by line to avoid memory issues
            while ((line = await reader.ReadLineAsync()) != null && matches.Count < maxMatches)
            {
                lineNumber++;
                lines.Add(line);
                
                // Keep only relevant lines in memory (sliding window)
                if (lines.Count > (contextLines * 2) + 100)
                {
                    lines.RemoveAt(0);
                }
                
                if (regex.IsMatch(line))
                {
                    var currentIndex = lines.Count - 1;
                    var contextStart = Math.Max(0, currentIndex - contextLines);
                    var contextEnd = Math.Min(lines.Count - 1, currentIndex + contextLines);
                    
                    matches.Add(new
                    {
                        LineNumber = lineNumber,
                        MatchedLine = line.Trim(),
                        Context = lines.Skip(contextStart).Take(contextEnd - contextStart + 1)
                                      .Select((contextLine, idx) => new {
                                          LineNum = lineNumber - (currentIndex - contextStart) + idx,
                                          Content = contextLine.Trim(),
                                          IsMatch = contextStart + idx == currentIndex
                                      }).ToArray(),
                        ExtractedGroups = ExtractRegexGroups(regex.Match(line))
                    });
                }
            }
            
            var fileInfo = new FileInfo(filePath);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                searchPattern = regexPattern,
                fileSize = fileInfo.Length,
                linesSearched = lineNumber,
                searchOptions = new { contextLines, caseSensitive, maxMatches, skipLines },
                totalMatches = matches.Count,
                matches = matches.ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Error searching file: {ex.Message}",
                filePath,
                exceptionType = ex.GetType().Name
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
    
    private static List<string> ExtractRegexGroups(Match match)
    {
        var extractedValues = new List<string>();
        
        for (int i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success)
            {
                extractedValues.Add(match.Groups[i].Value);
            }
        }
        
        return extractedValues;
    }
}