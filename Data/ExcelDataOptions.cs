namespace SegmentationApi.Data;

public sealed class ExcelDataOptions
{
    public const string SectionName = "ExcelData";

    public required string CustomerAnalysisPath { get; init; }

    public required string ProductCatalogPath { get; init; }
}
