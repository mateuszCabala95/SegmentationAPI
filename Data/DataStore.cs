namespace SegmentationApi.Data;

public sealed class DataStore(
    IReadOnlyList<FeatureDefinition> features,
    IReadOnlyList<Product> products,
    IReadOnlyDictionary<SegmentKey, SegmentScores> segments)
{
    private readonly ILookup<string, Product> _productsByAsin =
        products.ToLookup(p => p.Asin, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<FeatureDefinition> Features { get; } = features;

    public IReadOnlyList<Product> Products { get; } = products;

    public IReadOnlyList<Product> FindByAsin(string asin) => _productsByAsin[asin].ToList();

    public SegmentScores? GetSegment(SegmentKey key) => segments.GetValueOrDefault(key);
}
