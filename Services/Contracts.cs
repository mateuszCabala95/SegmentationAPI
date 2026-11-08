using SegmentationApi.Domain;

namespace SegmentationApi.Services;

public sealed record ProductDto(string Asin, string Name, string Category);

public sealed record RecommendationRequest(
    Gender Gender,
    int Age,
    IncomeBracket Income,
    string Asin,
    string? Category,
    int TopN = 1);

public sealed record SegmentLabelsDto(string Age, string Gender, string Income);

public sealed record DimensionValues(double? Age, double? Gender, double? Income);

public sealed record FeatureRecommendation(
    string FeatureId,
    string Name,
    string Definition,
    double AveragePriority,
    DimensionValues Priorities,
    DimensionValues Differences);

public sealed record RecommendationResponse(
    ProductDto Product,
    SegmentLabelsDto Segments,
    IReadOnlyList<SegmentDimension> MissingSegments,
    IReadOnlyList<FeatureRecommendation> TopFeatures);
