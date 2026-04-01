using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Mjm.LocalDocs.Tests.Documents;

/// <summary>
/// Tests to verify that the generated Excel test files contain the expected content.
/// </summary>
public sealed class VerifyExcelTestFilesTests
{
    private readonly string _testFilesDir = Path.Combine(
        Path.GetDirectoryName(typeof(VerifyExcelTestFilesTests).Assembly.Location)!,
        "Documents",
        "TestFiles");

    [Fact]
    public void VerifySampleXlsx_ContainsExpectedData()
    {
        // Arrange
        var filePath = Path.Combine(_testFilesDir, "sample.xlsx");

        // Act
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var workbook = new XSSFWorkbook(stream);
        var sheet = workbook.GetSheetAt(0);

        // Assert
        Assert.Equal("Sample Data", sheet.SheetName);
        
        // Verify headers
        var headerRow = sheet.GetRow(0);
        Assert.Equal("Name", headerRow.GetCell(0).StringCellValue);
        Assert.Equal("Department", headerRow.GetCell(1).StringCellValue);
        Assert.Equal("Salary", headerRow.GetCell(2).StringCellValue);
        Assert.Equal("Start Date", headerRow.GetCell(3).StringCellValue);

        // Verify first data row
        var row1 = sheet.GetRow(1);
        Assert.Equal("John Doe", row1.GetCell(0).StringCellValue);
        Assert.Equal("Engineering", row1.GetCell(1).StringCellValue);
        Assert.Equal(95000.0, row1.GetCell(2).NumericCellValue);
        Assert.Equal("2020-01-15", row1.GetCell(3).StringCellValue);

        // Verify row count
        Assert.Equal(3, sheet.LastRowNum); // 0-indexed, so row 3 is the 4th row (header + 3 data rows)
    }

    [Fact]
    public void VerifySampleXls_ContainsExpectedData()
    {
        // Arrange
        var filePath = Path.Combine(_testFilesDir, "sample.xls");

        // Act
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var workbook = new HSSFWorkbook(stream);
        var sheet = workbook.GetSheetAt(0);

        // Assert
        Assert.Equal("Sample Data", sheet.SheetName);
        
        // Verify headers
        var headerRow = sheet.GetRow(0);
        Assert.Equal("Name", headerRow.GetCell(0).StringCellValue);
        Assert.Equal("Department", headerRow.GetCell(1).StringCellValue);
        Assert.Equal("Salary", headerRow.GetCell(2).StringCellValue);
        Assert.Equal("Start Date", headerRow.GetCell(3).StringCellValue);

        // Verify first data row
        var row1 = sheet.GetRow(1);
        Assert.Equal("John Doe", row1.GetCell(0).StringCellValue);
        Assert.Equal("Engineering", row1.GetCell(1).StringCellValue);
        Assert.Equal(95000.0, row1.GetCell(2).NumericCellValue);
        Assert.Equal("2020-01-15", row1.GetCell(3).StringCellValue);

        // Verify row count
        Assert.Equal(3, sheet.LastRowNum);
    }

    [Fact]
    public void VerifyMultiSheetXlsx_ContainsAllSheets()
    {
        // Arrange
        var filePath = Path.Combine(_testFilesDir, "multi-sheet.xlsx");

        // Act
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var workbook = new XSSFWorkbook(stream);

        // Assert - Verify sheet count
        Assert.Equal(3, workbook.NumberOfSheets);
        Assert.Equal("Summary", workbook.GetSheetAt(0).SheetName);
        Assert.Equal("Details", workbook.GetSheetAt(1).SheetName);
        Assert.Equal("Notes", workbook.GetSheetAt(2).SheetName);

        // Sheet 1 - Summary
        var summarySheet = workbook.GetSheetAt(0);
        var summaryRow1 = summarySheet.GetRow(0);
        Assert.Equal("Total Employees", summaryRow1.GetCell(0).StringCellValue);
        Assert.Equal(3.0, summaryRow1.GetCell(1).NumericCellValue);

        var summaryRow2 = summarySheet.GetRow(1);
        Assert.Equal("Total Salary", summaryRow2.GetCell(0).StringCellValue);
        Assert.Equal(255000.0, summaryRow2.GetCell(1).NumericCellValue);

        // Sheet 2 - Details
        var detailsSheet = workbook.GetSheetAt(1);
        var headerRow = detailsSheet.GetRow(0);
        Assert.Equal("Name", headerRow.GetCell(0).StringCellValue);
        Assert.Equal("Department", headerRow.GetCell(1).StringCellValue);

        // Sheet 3 - Notes
        var notesSheet = workbook.GetSheetAt(2);
        var notesRow1 = notesSheet.GetRow(0);
        Assert.Equal("This workbook contains employee compensation data", notesRow1.GetCell(0).StringCellValue);

        var notesRow2 = notesSheet.GetRow(1);
        Assert.Equal("Last updated: 2026-04-01", notesRow2.GetCell(0).StringCellValue);
    }
}
