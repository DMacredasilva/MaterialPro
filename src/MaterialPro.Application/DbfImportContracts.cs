namespace MaterialPro.Application;

public enum DbfImportTable
{
    Unknown = 0,
    Clients = 1,
    Products = 2,
    Suppliers = 3,
    Stock = 4,
    Sales = 5,
    Receivable = 6,
    Payable = 7,
    Duplicates = 8
}

public enum DbfImportSelectionKind
{
    File = 1,
    Folder = 2
}

public sealed record DbfImportSelection(DbfImportSelectionKind Kind, string Path);
public sealed record DbfImportSourceFile(string FilePath, string FileName, DbfImportTable Table, int RecordCount, string VersionDescription, bool HasMemo);
public sealed record DbfFieldInfo(string Name, string Type, int Length, int DecimalCount);
public sealed record DbfFieldMapping(string SourceField, string TargetField, bool Enabled);
public sealed record DbfValidationIssue(string Severity, string Message, int? RowNumber = null);
public sealed record DbfImportScanResult(IReadOnlyList<DbfImportSourceFile> Files);
public sealed record DbfImportPreview(DbfImportSourceFile Source, IReadOnlyList<DbfFieldInfo> Fields, IReadOnlyList<DbfFieldMapping> SuggestedMappings);
public sealed record DbfImportValidationResult(
    DbfImportSourceFile Source,
    int RecordCount,
    IReadOnlyList<DbfValidationIssue> Issues);

public sealed record DbfImportRequest(
    IReadOnlyList<DbfImportSourceFile> Sources,
    IReadOnlyDictionary<string, IReadOnlyList<DbfFieldMapping>> MappingsByFile,
    bool UpdateExisting,
    bool IgnoreDuplicates,
    bool PartialImport,
    int? MaxRecords);

public sealed record DbfImportFileReport(
    string FileName,
    DbfImportTable Table,
    int TotalRecords,
    int ImportedRecords,
    int IgnoredRecords,
    int ErrorRecords,
    string BackupFolder,
    string LogFile);

public sealed record DbfImportResult(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    IReadOnlyList<DbfImportFileReport> Files,
    string SummaryLogFile);

public interface IDbfImportService
{
    DbfImportScanResult Scan(DbfImportSelection selection);
    DbfImportPreview Preview(string dbfPath);
    DbfImportValidationResult Validate(string dbfPath, IReadOnlyList<DbfFieldMapping> mappings, int? maxRecords = null);
    DbfImportResult Import(DbfImportRequest request);
}
