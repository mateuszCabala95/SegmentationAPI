using SegmentationApi.Domain;

namespace SegmentationApi.Data;

public sealed record FeatureDefinition(string Id, string Name, string Definition);

public sealed record Product(
    string Asin,
    string Name,
    string Category,
    string SheetName,
    IReadOnlySet<string> FeatureNames);

public sealed record SegmentKey(SegmentDimension Dimension, string Category, string Group);

/// <summary>
/// Feature scores for one demographic group within one category. Keys are feature names as used in Excel.
/// </summary>
public sealed record SegmentScores(
    int CustomerCount,
    IReadOnlyDictionary<string, double?> Priorities,
    IReadOnlyDictionary<string, double?> Differences);
