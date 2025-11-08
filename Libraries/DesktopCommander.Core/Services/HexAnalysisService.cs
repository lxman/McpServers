using System.Text;

namespace DesktopCommander.Core.Services;

/// <summary>
/// Service for analyzing binary files with hex operations
/// </summary>
public class HexAnalysisService(SecurityManager securityManager)
{
    /// <summary>
    /// Reads bytes from a file and returns them in hex format
    /// </summary>
    public HexDumpResult ReadHexBytes(string filePath, long offset, int length, HexFormat format = HexFormat.HexAscii)
    {
        var canonicalPath = Path.GetFullPath(filePath);
        securityManager.ValidateFileAccess(canonicalPath, FileAccessType.Read);

        if (!File.Exists(canonicalPath))
            throw new FileNotFoundException($"File not found: {canonicalPath}");

        var fileInfo = new FileInfo(canonicalPath);
        if (offset < 0 || offset >= fileInfo.Length)
            throw new ArgumentException($"Offset {offset} is outside file bounds (file size: {fileInfo.Length})");

        // Adjust length if it would exceed file size
        var maxLength = fileInfo.Length - offset;
        if (length > maxLength)
            length = (int)maxLength;

        var buffer = new byte[length];
        using (var stream = File.OpenRead(canonicalPath))
        {
            stream.Seek(offset, SeekOrigin.Begin);
            var bytesRead = stream.Read(buffer, 0, length);
            if (bytesRead < length)
                Array.Resize(ref buffer, bytesRead);
        }

        return new HexDumpResult
        {
            FilePath = canonicalPath,
            Offset = offset,
            Length = buffer.Length,
            FileSize = fileInfo.Length,
            Data = buffer,
            FormattedOutput = FormatHexDump(buffer, offset, format)
        };
    }

    /// <summary>
    /// Compares two binary files byte-by-byte
    /// </summary>
    public BinaryComparisonResult CompareBinaryFiles(
        string file1Path, 
        string file2Path,
        long offset = 0,
        int? length = null,
        bool showMatches = false)
    {
        var canonical1 = Path.GetFullPath(file1Path);
        var canonical2 = Path.GetFullPath(file2Path);

        securityManager.ValidateFileAccess(canonical1, FileAccessType.Read);
        securityManager.ValidateFileAccess(canonical2, FileAccessType.Read);

        if (!File.Exists(canonical1))
            throw new FileNotFoundException($"File 1 not found: {canonical1}");
        if (!File.Exists(canonical2))
            throw new FileNotFoundException($"File 2 not found: {canonical2}");

        var file1Info = new FileInfo(canonical1);
        var file2Info = new FileInfo(canonical2);

        // Determine comparison length
        var maxLength1 = file1Info.Length - offset;
        var maxLength2 = file2Info.Length - offset;
        var compareLength = length ?? (int)Math.Min(maxLength1, maxLength2);

        if (compareLength > maxLength1)
            compareLength = (int)maxLength1;
        if (compareLength > maxLength2)
            compareLength = (int)maxLength2;

        var buffer1 = new byte[compareLength];
        var buffer2 = new byte[compareLength];

        using (var stream1 = File.OpenRead(canonical1))
        using (var stream2 = File.OpenRead(canonical2))
        {
            stream1.Seek(offset, SeekOrigin.Begin);
            stream2.Seek(offset, SeekOrigin.Begin);

            stream1.ReadExactly(buffer1, 0, compareLength);
            stream2.ReadExactly(buffer2, 0, compareLength);
        }

        var differences = new List<ByteDifference>();
        var matchCount = 0;

        for (var i = 0; i < compareLength; i++)
        {
            if (buffer1[i] != buffer2[i])
            {
                differences.Add(new ByteDifference
                {
                    Offset = offset + i,
                    Byte1 = buffer1[i],
                    Byte2 = buffer2[i]
                });
            }
            else
            {
                matchCount++;
            }
        }

        return new BinaryComparisonResult
        {
            File1Path = canonical1,
            File2Path = canonical2,
            File1Size = file1Info.Length,
            File2Size = file2Info.Length,
            ComparisonOffset = offset,
            ComparisonLength = compareLength,
            TotalDifferences = differences.Count,
            TotalMatches = matchCount,
            Differences = showMatches ? differences : differences.Take(100).ToList(),
            IsTruncated = !showMatches && differences.Count > 100,
            FormattedComparison = FormatComparison(buffer1, buffer2, offset, differences, showMatches)
        };
    }

    /// <summary>
    /// Searches for a hex pattern in a file
    /// </summary>
    public HexSearchResult SearchHexPattern(
        string filePath,
        string hexPattern,
        long startOffset = 0,
        int maxResults = 100)
    {
        var canonicalPath = Path.GetFullPath(filePath);
        securityManager.ValidateFileAccess(canonicalPath, FileAccessType.Read);

        if (!File.Exists(canonicalPath))
            throw new FileNotFoundException($"File not found: {canonicalPath}");

        // Parse hex pattern (supports wildcards with ??)
        var pattern = ParseHexPattern(hexPattern, out var wildcards);

        var matches = new List<HexMatch>();
        var fileInfo = new FileInfo(canonicalPath);

        using (var stream = File.OpenRead(canonicalPath))
        {
            if (startOffset > 0)
                stream.Seek(startOffset, SeekOrigin.Begin);

            var buffer = new byte[64 * 1024]; // 64KB buffer
            int bytesRead;
            var currentOffset = startOffset;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0 && matches.Count < maxResults)
            {
                for (var i = 0; i <= bytesRead - pattern.Length; i++)
                {
                    if (!MatchesPattern(buffer, i, pattern, wildcards)) continue;
                    var match = new HexMatch
                    {
                        Offset = currentOffset + i,
                        MatchedBytes = buffer.Skip(i).Take(pattern.Length).ToArray()
                    };
                    matches.Add(match);

                    if (matches.Count >= maxResults)
                        break;
                }

                currentOffset += bytesRead;
            }
        }

        return new HexSearchResult
        {
            FilePath = canonicalPath,
            FileSize = fileInfo.Length,
            Pattern = hexPattern,
            StartOffset = startOffset,
            TotalMatches = matches.Count,
            Matches = matches,
            IsTruncated = matches.Count >= maxResults
        };
    }

    /// <summary>
    /// Generates a classic hex dump with addresses, hex values, and ASCII
    /// </summary>
    public string GenerateHexDump(
        string filePath,
        long offset = 0,
        int length = 512,
        int bytesPerLine = 16)
    {
        var result = ReadHexBytes(filePath, offset, length);
        return FormatHexDump(result.Data, offset, HexFormat.HexAscii, bytesPerLine);
    }

    #region Private Helper Methods

    private static string FormatHexDump(byte[] data, long startOffset, HexFormat format, int bytesPerLine = 16)
    {
        var sb = new StringBuilder();

        switch (format)
        {
            case HexFormat.HexOnly:
                for (var i = 0; i < data.Length; i++)
                {
                    sb.Append($"{data[i]:X2} ");
                    if ((i + 1) % bytesPerLine == 0)
                        sb.AppendLine();
                }
                break;

            case HexFormat.HexAscii:
                for (var i = 0; i < data.Length; i += bytesPerLine)
                {
                    var lineLength = Math.Min(bytesPerLine, data.Length - i);

                    // Address
                    sb.Append($"{startOffset + i:X8}:  ");

                    // Hex bytes
                    for (var j = 0; j < bytesPerLine; j++)
                    {
                        if (j < lineLength)
                            sb.Append($"{data[i + j]:X2} ");
                        else
                            sb.Append("   ");

                        if (j == 7)
                            sb.Append(" ");
                    }

                    sb.Append(" |");

                    // ASCII representation
                    for (var j = 0; j < lineLength; j++)
                    {
                        var b = data[i + j];
                        var c = (b >= 32 && b <= 126) ? (char)b : '.';
                        sb.Append(c);
                    }

                    sb.AppendLine("|");
                }
                break;

            case HexFormat.CArray:
                sb.AppendLine("byte[] data = {");
                for (var i = 0; i < data.Length; i++)
                {
                    if (i % bytesPerLine == 0)
                        sb.Append("    ");

                    sb.Append($"0x{data[i]:X2}");

                    if (i < data.Length - 1)
                        sb.Append(", ");

                    if ((i + 1) % bytesPerLine == 0)
                        sb.AppendLine();
                }
                sb.AppendLine("\r\n};");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }

        return sb.ToString();
    }

    private static string FormatComparison(
        byte[] buffer1,
        byte[] buffer2,
        long offset,
        List<ByteDifference> differences,
        bool showAll)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Offset     File1  File2  Diff");
        sb.AppendLine("--------   -----  -----  ----");

        var diffsToShow = showAll ? differences : differences.Take(50).ToList();

        foreach (var diff in diffsToShow)
        {
            sb.AppendLine($"{diff.Offset:X8}   {diff.Byte1:X2}     {diff.Byte2:X2}     {(diff.Byte1 > diff.Byte2 ? ">" : "<")}");
        }

        if (!showAll && differences.Count > 50)
        {
            sb.AppendLine($"... and {differences.Count - 50} more differences");
        }

        return sb.ToString();
    }

    private static byte[] ParseHexPattern(string hexPattern, out bool[] wildcards)
    {
        // Remove spaces and dashes
        hexPattern = hexPattern.Replace(" ", "").Replace("-", "").ToUpper();

        if (hexPattern.Length % 2 != 0)
            throw new ArgumentException("Hex pattern must have an even number of characters");

        var byteCount = hexPattern.Length / 2;
        var pattern = new byte[byteCount];
        wildcards = new bool[byteCount];

        for (var i = 0; i < byteCount; i++)
        {
            var byteStr = hexPattern.Substring(i * 2, 2);
            if (byteStr == "??")
            {
                wildcards[i] = true;
                pattern[i] = 0;
            }
            else
            {
                wildcards[i] = false;
                pattern[i] = Convert.ToByte(byteStr, 16);
            }
        }

        return pattern;
    }

    private static bool MatchesPattern(byte[] buffer, int offset, byte[] pattern, bool[] wildcards)
    {
        return !pattern.Where((t, i) => !wildcards[i] && buffer[offset + i] != t).Any();
    }

    #endregion
}

#region Result Classes

public class HexDumpResult
{
    public string FilePath { get; set; } = string.Empty;
    public long Offset { get; set; }
    public int Length { get; set; }
    public long FileSize { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string FormattedOutput { get; set; } = string.Empty;
}

public class BinaryComparisonResult
{
    public string File1Path { get; set; } = string.Empty;
    public string File2Path { get; set; } = string.Empty;
    public long File1Size { get; set; }
    public long File2Size { get; set; }
    public long ComparisonOffset { get; set; }
    public int ComparisonLength { get; set; }
    public int TotalDifferences { get; set; }
    public int TotalMatches { get; set; }
    public List<ByteDifference> Differences { get; set; } = new();
    public bool IsTruncated { get; set; }
    public string FormattedComparison { get; set; } = string.Empty;
}

public class ByteDifference
{
    public long Offset { get; set; }
    public byte Byte1 { get; set; }
    public byte Byte2 { get; set; }
}

public class HexSearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public long StartOffset { get; set; }
    public int TotalMatches { get; set; }
    public List<HexMatch> Matches { get; set; } = new();
    public bool IsTruncated { get; set; }
}

public class HexMatch
{
    public long Offset { get; set; }
    public byte[] MatchedBytes { get; set; } = Array.Empty<byte>();
}

public enum HexFormat
{
    HexOnly,
    HexAscii,
    CArray
}

#endregion
