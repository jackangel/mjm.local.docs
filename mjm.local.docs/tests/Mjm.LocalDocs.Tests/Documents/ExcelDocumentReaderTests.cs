using Mjm.LocalDocs.Infrastructure.Documents;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;

namespace Mjm.LocalDocs.Tests.Documents;

/// <summary>
/// Unit tests for <see cref="ExcelDocumentReader"/>.
/// </summary>
public sealed class ExcelDocumentReaderTests
{
    private readonly ExcelDocumentReader _sut = new();

    #region SupportedExtensions Tests

    [Fact]
    public void SupportedExtensions_ReturnsBothXlsAndXlsx()
    {
        // Act
        var extensions = _sut.SupportedExtensions;

        // Assert
        Assert.Equal(2, extensions.Count);
        Assert.Contains(".xls", extensions);
        Assert.Contains(".xlsx", extensions);
    }

    #endregion

    #region CanRead Tests

    [Theory]
    [InlineData(".xls", true)]
    [InlineData(".XLS", true)]
    [InlineData(".Xls", true)]
    [InlineData(".xlsx", true)]
    [InlineData(".XLSX", true)]
    [InlineData(".Xlsx", true)]
    [InlineData(".doc", false)]
    [InlineData(".docx", false)]
    [InlineData(".txt", false)]
    [InlineData(".pdf", false)]
    [InlineData("", false)]
    public void CanRead_ReturnsExpectedResult(string extension, bool expected)
    {
        // Act
        var result = _sut.CanRead(extension);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region ExtractTextAsync Tests - Empty/Invalid

    [Fact]
    public async Task ExtractTextAsync_WithEmptyFile_ReturnsFailure()
    {
        // Arrange
        var fileContent = Array.Empty<byte>();

        // Act
        var result = await _sut.ExtractTextAsync(fileContent);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.Text);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractTextAsync_WithInvalidDocument_ReturnsFailure()
    {
        // Arrange - random bytes that are not a valid Excel document
        var fileContent = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD };

        // Act
        var result = await _sut.ExtractTextAsync(fileContent);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.Text);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithCorruptedExcel_ReturnsFailureWithHelpfulMessage()
    {
        // Arrange - ZIP signature but corrupted content
        var fileContent = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00 };

        // Act
        var result = await _sut.ExtractTextAsync(fileContent);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.Text);
        Assert.NotNull(result.ErrorMessage);
    }

    #endregion

    #region ExtractTextAsync Tests - .xlsx Format

    [Fact]
    public async Task ExtractTextAsync_WithValidXlsxContainingText_ReturnsSuccess()
    {
        // Arrange
        var xlsxContent = CreateXlsxWithText("Hello World from XLSX");

        // Act
        var result = await _sut.ExtractTextAsync(xlsxContent);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Hello", result.Text);
        Assert.Contains("World", result.Text);
        Assert.Contains("XLSX", result.Text);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithMultiSheetXlsx_ExtractsAllSheetsWithHeaders()
    {
        // Arrange
        var xlsxContent = CreateXlsxWithMultipleSheets(
            ("Sheet1", "First sheet content"),
            ("Sheet2", "Second sheet content"),
            ("Summary", "Summary content"));

        // Act
        var result = await _sut.ExtractTextAsync(xlsxContent);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("=== Sheet: Sheet1 ===", result.Text);
        Assert.Contains("First sheet content", result.Text);
        Assert.Contains("=== Sheet: Sheet2 ===", result.Text);
        Assert.Contains("Second sheet content", result.Text);
        Assert.Contains("=== Sheet: Summary ===", result.Text);
        Assert.Contains("Summary content", result.Text);
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyXlsx_ReturnsFailure()
    {
        // Arrange - valid xlsx structure but no content
        var xlsxContent = CreateEmptyXlsx();

        // Act
        var result = await _sut.ExtractTextAsync(xlsxContent);

        // Assert
        Assert.False(result.Success);
        Assert.Empty(result.Text);
        Assert.Contains("No extractable text", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithXlsxContainingNumericValues_ExtractsAsStrings()
    {
        // Arrange
        var xlsxContent = CreateXlsxWithNumericData();

        // Act
        var result = await _sut.ExtractTextAsync(xlsxContent);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("42", result.Text);
        Assert.Contains("3.14", result.Text);
    }

    [Fact]
    public async Task ExtractTextAsync_WithXlsxContainingFormulas_ExtractsCachedValues()
    {
        // Arrange
        var xlsxContent = CreateXlsxWithFormula();

        // Act
        var result = await _sut.ExtractTextAsync(xlsxContent);

        // Assert
        Assert.True(result.Success);
        // Formula =2+2 should have cached value of 4
        Assert.Contains("4", result.Text);
    }

    #endregion

    #region ExtractTextAsync Tests - .xls Format

    [Fact]
    public async Task ExtractTextAsync_WithValidXlsContainingText_ReturnsSuccess()
    {
        // Arrange
        var xlsContent = CreateXlsWithText("Hello World from XLS");

        // Act
        var result = await _sut.ExtractTextAsync(xlsContent);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Hello", result.Text);
        Assert.Contains("World", result.Text);
        Assert.Contains("XLS", result.Text);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_WithMultiSheetXls_ExtractsAllSheets()
    {
        // Arrange
        var xlsContent = CreateXlsWithMultipleSheets(
            ("Sales", "Sales data content"),
            ("Revenue", "Revenue data content"));

        // Act
        var result = await _sut.ExtractTextAsync(xlsContent);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("=== Sheet: Sales ===", result.Text);
        Assert.Contains("Sales data content", result.Text);
        Assert.Contains("=== Sheet: Revenue ===", result.Text);
        Assert.Contains("Revenue data content", result.Text);
    }

    [Fact]
    public async Task ExtractTextAsync_WithXlsContainingMixedContent_ExtractsAll()
    {
        // Arrange
        var xlsContent = CreateXlsWithMixedContent();

        // Act
        var result = await _sut.ExtractTextAsync(xlsContent);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Product Name", result.Text);
        Assert.Contains("100", result.Text);
        Assert.Contains("Widget", result.Text);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExtractTextAsync_SupportsCancellation()
    {
        // Arrange
        var xlsxContent = CreateXlsxWithText("Test content");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ExtractTextAsync(xlsxContent, cts.Token));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a valid .xlsx file containing the specified text.
    /// </summary>
    private static byte[] CreateXlsxWithText(string text)
    {
        using var stream = new MemoryStream();
        using (var workbook = new XSSFWorkbook())
        {
            var sheet = workbook.CreateSheet("Sheet1");
            var row = sheet.CreateRow(0);
            var cell = row.CreateCell(0);
            cell.SetCellValue(text);
            workbook.Write(stream);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a valid .xlsx file with multiple sheets.
    /// </summary>
    private static byte[] CreateXlsxWithMultipleSheets(params (string sheetName, string content)[] sheets)
    {
        using var stream = new MemoryStream();
        using (var workbook = new XSSFWorkbook())
        {
            foreach (var (sheetName, content) in sheets)
            {
                var sheet = workbook.CreateSheet(sheetName);
                var row = sheet.CreateRow(0);
                var cell = row.CreateCell(0);
                cell.SetCellValue(content);
            }
            workbook.Write(stream);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a valid .xls file containing the specified text.
    /// </summary>
    private static byte[] CreateXlsWithText(string text)
    {
        using var stream = new MemoryStream();
        using (var workbook = new HSSFWorkbook())
        {
            var sheet = workbook.CreateSheet("Sheet1");
            var row = sheet.CreateRow(0);
            var cell = row.CreateCell(0);
            cell.SetCellValue(text);
            workbook.Write(stream);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a valid .xls file with multiple sheets.
    /// </summary>
    private static byte[] CreateXlsWithMultipleSheets(params (string sheetName, string content)[] sheets)
    {
        using var stream = new MemoryStream();
        using (var workbook = new HSSFWorkbook())
        {
            foreach (var (sheetName, content) in sheets)
            {
                var sheet = workbook.CreateSheet(sheetName);
                var row = sheet.CreateRow(0);
                var cell = row.CreateCell(0);
                cell.SetCellValue(content);
            }
            workbook.Write(stream);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Creates an empty .xlsx file (valid structure but no text).
    /// </summary>
    private static byte[] CreateEmptyXlsx()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XSSFWorkbook())
        {
            // Create sheet but leave it empty (no rows)
            workbook.CreateSheet("Sheet1");
            workbook.Write(stream);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a .xlsx file with numeric data.
    /// </summary>
    private static byte[] CreateXlsxWithNumericData()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XSSFWorkbook())
        {
            var sheet = workbook.CreateSheet("Numbers");
            var row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue(42);
            row.CreateCell(1).SetCellValue(3.14);
            workbook.Write(stream);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a .xlsx file with formulas.
    /// </summary>
    private static byte[] CreateXlsxWithFormula()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XSSFWorkbook())
        {
            var sheet = workbook.CreateSheet("Formulas");
            var row = sheet.CreateRow(0);
            var cell = row.CreateCell(0);
            cell.SetCellFormula("2+2");
            // Set cached value so it can be extracted
            cell.SetCellValue(4);
            workbook.Write(stream);
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a .xls file with mixed numeric and string content.
    /// </summary>
    private static byte[] CreateXlsWithMixedContent()
    {
        using var stream = new MemoryStream();
        using (var workbook = new HSSFWorkbook())
        {
            var sheet = workbook.CreateSheet("MixedData");
            var row0 = sheet.CreateRow(0);
            row0.CreateCell(0).SetCellValue("Product Name");
            row0.CreateCell(1).SetCellValue("Quantity");
            
            var row1 = sheet.CreateRow(1);
            row1.CreateCell(0).SetCellValue("Widget");
            row1.CreateCell(1).SetCellValue(100);
            
            workbook.Write(stream);
        }
        return stream.ToArray();
    }

    #endregion
}
