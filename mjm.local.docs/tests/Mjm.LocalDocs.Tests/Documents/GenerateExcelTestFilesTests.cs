namespace Mjm.LocalDocs.Tests.Documents;

/// <summary>
/// Test class to generate Excel test files.
/// Run this test to create the physical Excel files needed for validation.
/// </summary>
public sealed class GenerateExcelTestFilesTests
{
    [Fact]
    public void GenerateExcelTestFiles()
    {
        // Act
        ExcelTestFileGenerator.GenerateAllTestFiles();

        // Assert
        var testFilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documents", "TestFiles");
        
        var sampleXlsx = Path.Combine(testFilesDir, "sample.xlsx");
        var sampleXls = Path.Combine(testFilesDir, "sample.xls");
        var multiSheet = Path.Combine(testFilesDir, "multi-sheet.xlsx");

        Assert.True(File.Exists(sampleXlsx), "sample.xlsx should exist");
        Assert.True(File.Exists(sampleXls), "sample.xls should exist");
        Assert.True(File.Exists(multiSheet), "multi-sheet.xlsx should exist");

        // Verify files have content
        Assert.True(new FileInfo(sampleXlsx).Length > 0, "sample.xlsx should not be empty");
        Assert.True(new FileInfo(sampleXls).Length > 0, "sample.xls should not be empty");
        Assert.True(new FileInfo(multiSheet).Length > 0, "multi-sheet.xlsx should not be empty");
    }
}
