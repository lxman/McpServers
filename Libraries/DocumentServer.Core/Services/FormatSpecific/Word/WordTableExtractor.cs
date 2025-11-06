using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.Core;
using DocumentServer.Core.Services.FormatSpecific.Word.Models;
using Microsoft.Extensions.Logging;
using MsOfficeCrypto;

namespace DocumentServer.Core.Services.FormatSpecific.Word;

/// <summary>
/// Service for extracting tables from Word documents
/// </summary>
public class WordTableExtractor(
    ILogger<WordTableExtractor> logger,
    DocumentCache cache,
    PasswordManager passwordManager)
{
    /// <summary>
    /// Extract all tables from a Word document
    /// </summary>
    public async Task<ServiceResult<List<WordTable>>> ExtractAllTablesAsync(string filePath)
    {
        logger.LogInformation("Extracting all tables from: {FilePath}", filePath);

        try
        {
            using WordprocessingDocument doc = await OpenDocumentAsync(filePath);
            Body? body = doc.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                return ServiceResult<List<WordTable>>.CreateFailure("Document body not found");
            }

            var tables = new List<WordTable>();
            var tableNumber = 1;

            foreach (Table table in body.Elements<Table>())
            {
                WordTable wordTable = ProcessTable(table, tableNumber++);
                tables.Add(wordTable);
            }

            logger.LogInformation("Extracted {Count} tables from: {FilePath}", tables.Count, filePath);

            return ServiceResult<List<WordTable>>.CreateSuccess(tables);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting tables from: {FilePath}", filePath);
            return ServiceResult<List<WordTable>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract a specific table by its number (1-based index)
    /// </summary>
    public async Task<ServiceResult<WordTable>> ExtractTableByNumberAsync(string filePath, int tableNumber)
    {
        logger.LogInformation("Extracting table #{Number} from: {FilePath}", tableNumber, filePath);

        try
        {
            using WordprocessingDocument doc = await OpenDocumentAsync(filePath);
            Body? body = doc.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                return ServiceResult<WordTable>.CreateFailure("Document body not found");
            }

            List<Table> allTables = body.Elements<Table>().ToList();

            if (tableNumber < 1 || tableNumber > allTables.Count)
            {
                return ServiceResult<WordTable>.CreateFailure(
                    $"Table {tableNumber} not found (document has {allTables.Count} tables)");
            }

            Table table = allTables[tableNumber - 1];
            WordTable wordTable = ProcessTable(table, tableNumber);

            logger.LogInformation("Extracted table #{Number} with {Rows} rows and {Cols} columns",
                tableNumber, wordTable.RowCount, wordTable.ColumnCount);

            return ServiceResult<WordTable>.CreateSuccess(wordTable);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting table #{Number} from: {FilePath}", tableNumber, filePath);
            return ServiceResult<WordTable>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get table count in document
    /// </summary>
    public async Task<ServiceResult<int>> GetTableCountAsync(string filePath)
    {
        logger.LogInformation("Getting table count from: {FilePath}", filePath);

        try
        {
            using WordprocessingDocument doc = await OpenDocumentAsync(filePath);
            Body? body = doc.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                return ServiceResult<int>.CreateFailure("Document body not found");
            }

            int count = body.Elements<Table>().Count();

            logger.LogInformation("Document has {Count} tables: {FilePath}", count, filePath);

            return ServiceResult<int>.CreateSuccess(count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting table count from: {FilePath}", filePath);
            return ServiceResult<int>.CreateFailure(ex);
        }
    }

    #region Private Methods

    private async Task<WordprocessingDocument> OpenDocumentAsync(string filePath)
    {
        LoadedDocument? cached = cache.Get(filePath);
        var doc = cached?.DocumentObject as WordprocessingDocument;

        if (doc is not null)
        {
            return doc;
        }

        string? password = passwordManager.GetPasswordForFile(filePath);

        // Use MsOfficeCrypto to handle decryption (or pass through if not encrypted)
        await using FileStream fileStream = File.OpenRead(filePath);
        await using Stream decryptedStream = await OfficeDocument.DecryptAsync(fileStream, password);
        
        // Copy to memory stream and open document
        var memoryStream = new MemoryStream();
        await decryptedStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return WordprocessingDocument.Open(memoryStream, false);
    }

    private static WordTable ProcessTable(Table table, int tableNumber)
    {
        var tableContent = new StringBuilder();
        var cells = new List<List<string>>();
        var headers = new List<string>();
        var firstRow = true;

        foreach (TableRow row in table.Elements<TableRow>())
        {
            var rowCells = new List<string>();

            foreach (TableCell cell in row.Elements<TableCell>())
            {
                var cellText = new StringBuilder();
                foreach (Paragraph paragraph in cell.Elements<Paragraph>())
                {
                    cellText.Append(GetParagraphText(paragraph).Trim());
                    cellText.Append(' ');
                }

                string cellContent = cellText.ToString().Trim();
                rowCells.Add(cellContent);
            }

            // First row is often headers
            if (firstRow && rowCells.Count > 0)
            {
                headers.AddRange(rowCells);
                firstRow = false;
            }

            cells.Add(rowCells);

            // Build content representation
            string rowString = string.Join(" | ", rowCells);
            tableContent.AppendLine(rowString);
        }

        int rowCount = cells.Count;
        int columnCount = cells.Count > 0 ? cells[0].Count : 0;

        return new WordTable
        {
            TableNumber = tableNumber,
            RowCount = rowCount,
            ColumnCount = columnCount,
            Cells = cells,
            Headers = headers,
            Content = tableContent.ToString().Trim()
        };
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var textBuilder = new StringBuilder();

        foreach (Run run in paragraph.Elements<Run>())
        {
            foreach (OpenXmlElement element in run.Elements())
            {
                if (element is Text text)
                {
                    textBuilder.Append(text.Text);
                }
                else if (element is TabChar)
                {
                    textBuilder.Append('\t');
                }
            }
        }

        return textBuilder.ToString();
    }

    #endregion
}