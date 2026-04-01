using System.Text;
using Mjm.LocalDocs.Core.Abstractions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Mjm.LocalDocs.Infrastructure.Documents;

/// <summary>
/// Document reader for Microsoft Excel files (.xls and .xlsx) using NPOI library.
/// </summary>
/// <remarks>
/// This reader extracts text content from Excel workbooks including:
/// - All sheets in the workbook (not just the first sheet)
/// - All cells with content (strings, numbers, formulas, booleans)
/// - Sheet names as section headers
/// 
/// Supports both legacy .xls (HSSF) and modern .xlsx (XSSF) formats.
/// </remarks>
public sealed class ExcelDocumentReader : IDocumentReader
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => [".xls", ".xlsx"];

    /// <inheritdoc />
    public bool CanRead(string fileExtension)
        => fileExtension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
           fileExtension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<TextExtractionResult> ExtractTextAsync(
        byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        if (fileContent.Length == 0)
        {
            return Task.FromResult(TextExtractionResult.Fail(
                "The Excel document is empty."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractedText = IsXlsxFormat(fileContent)
                ? ExtractFromXlsx(fileContent, cancellationToken)
                : ExtractFromXls(fileContent, cancellationToken);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return Task.FromResult(TextExtractionResult.Fail(
                    "No extractable text found in the Excel document. " +
                    "All sheets may be empty or contain only non-text elements."));
            }

            return Task.FromResult(TextExtractionResult.Ok(extractedText));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(TextExtractionResult.Fail(
                $"Failed to read Excel document: {ex.Message}"));
        }
    }

    /// <summary>
    /// Extracts text from a .xlsx file (Office Open XML format).
    /// </summary>
    private static string ExtractFromXlsx(byte[] fileContent, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(fileContent);
        using var workbook = new XSSFWorkbook(stream);
        
        cancellationToken.ThrowIfCancellationRequested();
        
        return ExtractFromWorkbook(workbook, cancellationToken);
    }

    /// <summary>
    /// Extracts text from a .xls file (legacy binary format).
    /// </summary>
    private static string ExtractFromXls(byte[] fileContent, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(fileContent);
        using var workbook = new HSSFWorkbook(stream);
        
        cancellationToken.ThrowIfCancellationRequested();
        
        return ExtractFromWorkbook(workbook, cancellationToken);
    }

    /// <summary>
    /// Extracts text from all sheets in a workbook.
    /// </summary>
    /// <remarks>
    /// Iterates through all sheets and extracts content from each.
    /// Sheet names are added as section headers.
    /// </remarks>
    private static string ExtractFromWorkbook(IWorkbook workbook, CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        var numberOfSheets = workbook.NumberOfSheets;
        var hasAnyContent = false;

        for (var i = 0; i < numberOfSheets; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var sheet = workbook.GetSheetAt(i);
            var sheetName = sheet.SheetName;

            // Extract sheet content
            var sheetText = ExtractSheetText(sheet, cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(sheetText))
            {
                hasAnyContent = true;
                
                // Add sheet header
                if (result.Length > 0)
                    result.AppendLine(); // Add blank line between sheets
                
                result.AppendLine($"=== Sheet: {sheetName} ===");
                result.AppendLine(sheetText);
            }
        }

        // Return empty string if no actual content was found
        return hasAnyContent ? result.ToString().Trim() : string.Empty;
    }

    /// <summary>
    /// Extracts text from a single sheet.
    /// </summary>
    /// <remarks>
    /// Iterates through all rows and cells, extracting text content.
    /// Handles different cell types: strings, numbers, formulas, booleans.
    /// </remarks>
    private static string ExtractSheetText(ISheet sheet, CancellationToken cancellationToken)
    {
        var result = new StringBuilder();

        // Iterate through all rows
        for (var rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var row = sheet.GetRow(rowIndex);
            if (row == null)
                continue;

            var rowText = new StringBuilder();

            // Iterate through all cells in the row
            for (var cellIndex = row.FirstCellNum; cellIndex < row.LastCellNum; cellIndex++)
            {
                var cell = row.GetCell(cellIndex);
                if (cell == null)
                    continue;

                var cellText = ExtractCellText(cell);
                if (!string.IsNullOrWhiteSpace(cellText))
                {
                    if (rowText.Length > 0)
                        rowText.Append(' '); // Add space between cells
                    
                    rowText.Append(cellText);
                }
            }

            // Add row text if not empty
            if (rowText.Length > 0)
            {
                result.AppendLine(rowText.ToString());
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Extracts text from a single cell based on its type.
    /// </summary>
    /// <remarks>
    /// Handles different cell types:
    /// - String: extracted as-is
    /// - Numeric: converted to string (includes dates)
    /// - Formula: extracts cached result value
    /// - Boolean: converted to "true" or "false"
    /// - Blank: returns empty string
    /// </remarks>
    private static string ExtractCellText(ICell cell)
    {
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric => cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue.ToString().ToLowerInvariant(),
            CellType.Formula => ExtractFormulaCellText(cell),
            CellType.Blank => string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Extracts the cached result value from a formula cell.
    /// </summary>
    /// <remarks>
    /// Formula cells store both the formula and the cached result.
    /// We extract the cached result value, not the formula text itself.
    /// </remarks>
    private static string ExtractFormulaCellText(ICell cell)
    {
        try
        {
            return cell.CachedFormulaResultType switch
            {
                CellType.String => cell.StringCellValue,
                CellType.Numeric => cell.NumericCellValue.ToString(),
                CellType.Boolean => cell.BooleanCellValue.ToString().ToLowerInvariant(),
                _ => string.Empty
            };
        }
        catch
        {
            // If we can't get the cached result, return empty
            return string.Empty;
        }
    }

    /// <summary>
    /// Determines if the file content is in .xlsx format (ZIP-based Office Open XML).
    /// </summary>
    /// <remarks>
    /// .xlsx files are ZIP archives and start with the ZIP signature (PK).
    /// .xls files are OLE2 compound documents and start with D0 CF 11 E0.
    /// </remarks>
    private static bool IsXlsxFormat(byte[] fileContent)
    {
        if (fileContent.Length < 4)
            return false;

        // ZIP signature: 50 4B 03 04 (PK..)
        return fileContent[0] == 0x50 && 
               fileContent[1] == 0x4B && 
               fileContent[2] == 0x03 && 
               fileContent[3] == 0x04;
    }
}
