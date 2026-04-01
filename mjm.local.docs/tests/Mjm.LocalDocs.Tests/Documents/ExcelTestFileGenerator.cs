using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Mjm.LocalDocs.Tests.Documents;

/// <summary>
/// Utility class to generate test Excel files for manual validation and integration testing.
/// </summary>
public static class ExcelTestFileGenerator
{
    /// <summary>
    /// Generates all test Excel files in the TestFiles directory.
    /// </summary>
    public static void GenerateAllTestFiles()
    {
        var testFilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documents", "TestFiles");
        Directory.CreateDirectory(testFilesDir);

        GenerateSampleXlsx(testFilesDir);
        GenerateSampleXls(testFilesDir);
        GenerateMultiSheetXlsx(testFilesDir);
    }

    /// <summary>
    /// Creates sample.xlsx - Simple single-sheet .xlsx file for basic validation.
    /// </summary>
    private static void GenerateSampleXlsx(string directory)
    {
        var filePath = Path.Combine(directory, "sample.xlsx");

        using var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Sample Data");

        // Header row
        var headerRow = sheet.CreateRow(0);
        headerRow.CreateCell(0).SetCellValue("Name");
        headerRow.CreateCell(1).SetCellValue("Department");
        headerRow.CreateCell(2).SetCellValue("Salary");
        headerRow.CreateCell(3).SetCellValue("Start Date");

        // Data rows
        var row1 = sheet.CreateRow(1);
        row1.CreateCell(0).SetCellValue("John Doe");
        row1.CreateCell(1).SetCellValue("Engineering");
        row1.CreateCell(2).SetCellValue(95000);
        row1.CreateCell(3).SetCellValue("2020-01-15");

        var row2 = sheet.CreateRow(2);
        row2.CreateCell(0).SetCellValue("Jane Smith");
        row2.CreateCell(1).SetCellValue("Marketing");
        row2.CreateCell(2).SetCellValue(78000);
        row2.CreateCell(3).SetCellValue("2019-06-01");

        var row3 = sheet.CreateRow(3);
        row3.CreateCell(0).SetCellValue("Bob Johnson");
        row3.CreateCell(1).SetCellValue("Sales");
        row3.CreateCell(2).SetCellValue(82000);
        row3.CreateCell(3).SetCellValue("2021-03-10");

        // Auto-size columns
        for (int i = 0; i < 4; i++)
        {
            sheet.AutoSizeColumn(i);
        }

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        workbook.Write(stream);

        Console.WriteLine($"Created: {filePath}");
    }

    /// <summary>
    /// Creates sample.xls - Same content as sample.xlsx but in legacy .xls format.
    /// </summary>
    private static void GenerateSampleXls(string directory)
    {
        var filePath = Path.Combine(directory, "sample.xls");

        using var workbook = new HSSFWorkbook();
        var sheet = workbook.CreateSheet("Sample Data");

        // Header row
        var headerRow = sheet.CreateRow(0);
        headerRow.CreateCell(0).SetCellValue("Name");
        headerRow.CreateCell(1).SetCellValue("Department");
        headerRow.CreateCell(2).SetCellValue("Salary");
        headerRow.CreateCell(3).SetCellValue("Start Date");

        // Data rows
        var row1 = sheet.CreateRow(1);
        row1.CreateCell(0).SetCellValue("John Doe");
        row1.CreateCell(1).SetCellValue("Engineering");
        row1.CreateCell(2).SetCellValue(95000);
        row1.CreateCell(3).SetCellValue("2020-01-15");

        var row2 = sheet.CreateRow(2);
        row2.CreateCell(0).SetCellValue("Jane Smith");
        row2.CreateCell(1).SetCellValue("Marketing");
        row2.CreateCell(2).SetCellValue(78000);
        row2.CreateCell(3).SetCellValue("2019-06-01");

        var row3 = sheet.CreateRow(3);
        row3.CreateCell(0).SetCellValue("Bob Johnson");
        row3.CreateCell(1).SetCellValue("Sales");
        row3.CreateCell(2).SetCellValue(82000);
        row3.CreateCell(3).SetCellValue("2021-03-10");

        // Auto-size columns
        for (int i = 0; i < 4; i++)
        {
            sheet.AutoSizeColumn(i);
        }

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        workbook.Write(stream);

        Console.WriteLine($"Created: {filePath}");
    }

    /// <summary>
    /// Creates multi-sheet.xlsx - .xlsx file with multiple sheets containing different content.
    /// </summary>
    private static void GenerateMultiSheetXlsx(string directory)
    {
        var filePath = Path.Combine(directory, "multi-sheet.xlsx");

        using var workbook = new XSSFWorkbook();

        // Sheet 1 - Summary
        var summarySheet = workbook.CreateSheet("Summary");
        var summaryRow1 = summarySheet.CreateRow(0);
        summaryRow1.CreateCell(0).SetCellValue("Total Employees");
        summaryRow1.CreateCell(1).SetCellValue(3);

        var summaryRow2 = summarySheet.CreateRow(1);
        summaryRow2.CreateCell(0).SetCellValue("Total Salary");
        summaryRow2.CreateCell(1).SetCellValue(255000);

        var summaryRow3 = summarySheet.CreateRow(2);
        summaryRow3.CreateCell(0).SetCellValue("Average Salary");
        summaryRow3.CreateCell(1).SetCellValue(85000);

        summarySheet.AutoSizeColumn(0);
        summarySheet.AutoSizeColumn(1);

        // Sheet 2 - Details
        var detailsSheet = workbook.CreateSheet("Details");
        var headerRow = detailsSheet.CreateRow(0);
        headerRow.CreateCell(0).SetCellValue("Name");
        headerRow.CreateCell(1).SetCellValue("Department");
        headerRow.CreateCell(2).SetCellValue("Salary");
        headerRow.CreateCell(3).SetCellValue("Start Date");

        var row1 = detailsSheet.CreateRow(1);
        row1.CreateCell(0).SetCellValue("John Doe");
        row1.CreateCell(1).SetCellValue("Engineering");
        row1.CreateCell(2).SetCellValue(95000);
        row1.CreateCell(3).SetCellValue("2020-01-15");

        var row2 = detailsSheet.CreateRow(2);
        row2.CreateCell(0).SetCellValue("Jane Smith");
        row2.CreateCell(1).SetCellValue("Marketing");
        row2.CreateCell(2).SetCellValue(78000);
        row2.CreateCell(3).SetCellValue("2019-06-01");

        var row3 = detailsSheet.CreateRow(3);
        row3.CreateCell(0).SetCellValue("Bob Johnson");
        row3.CreateCell(1).SetCellValue("Sales");
        row3.CreateCell(2).SetCellValue(82000);
        row3.CreateCell(3).SetCellValue("2021-03-10");

        for (int i = 0; i < 4; i++)
        {
            detailsSheet.AutoSizeColumn(i);
        }

        // Sheet 3 - Notes
        var notesSheet = workbook.CreateSheet("Notes");
        var notesRow1 = notesSheet.CreateRow(0);
        notesRow1.CreateCell(0).SetCellValue("This workbook contains employee compensation data");

        var notesRow2 = notesSheet.CreateRow(1);
        notesRow2.CreateCell(0).SetCellValue("Last updated: 2026-04-01");

        notesSheet.AutoSizeColumn(0);

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        workbook.Write(stream);

        Console.WriteLine($"Created: {filePath}");
    }
}
