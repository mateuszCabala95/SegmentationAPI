using SegmentationApi.Data;
using SegmentationApi.Domain;

namespace SegmentationApi.Services;

public abstract record RecommendationResult
{
    public sealed record Success(RecommendationResponse Response) : RecommendationResult;

    public sealed record ValidationFailed(IDictionary<string, string[]> Errors) : RecommendationResult;

    public sealed record NotFound(string Message) : RecommendationResult;
}

public sealed class RecommendationService(DataStore store)
{
    public const int MinTopN = 1;
    public const int MaxTopN = 2;

    public IReadOnlyList<ProductDto> GetProducts(string? category) =>
        store.Products
            .Where(p => category is null || p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(p => (p.Asin, p.Category))
            .OrderBy(p => p.Category, StringComparer.Ordinal)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ProductDto(p.Asin, p.Name, p.Category))
            .ToList();

    public RecommendationResult Recommend(RecommendationRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return new RecommendationResult.ValidationFailed(errors);
        }

        var candidates = store.FindByAsin(request.Asin.Trim());
        if (request.Category is { Length: > 0 } category)
        {
            candidates = candidates
                .Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return new RecommendationResult.NotFound(
                $"Product with ASIN '{request.Asin}'{(request.Category is { Length: > 0 } ? $" in category '{request.Category}'" : "")} was not found.");
        }

        var distinctCategories = candidates.Select(p => p.Category).Distinct().ToList();
        if (distinctCategories.Count > 1)
        {
            return new RecommendationResult.ValidationFailed(new Dictionary<string, string[]>
            {
                [nameof(RecommendationRequest.Category)] =
                    [$"ASIN '{request.Asin}' exists in several categories; specify one of: {string.Join(", ", distinctCategories)}."]
            });
        }

        var product = candidates[0];
        var labels = new SegmentLabelsDto(
            SegmentLabels.ForAge(request.Age),
            SegmentLabels.ForGender(request.Gender),
            SegmentLabels.ForIncome(request.Income));

        var ageScores = store.GetSegment(new SegmentKey(SegmentDimension.Age, product.Category, labels.Age));
        var genderScores = store.GetSegment(new SegmentKey(SegmentDimension.Gender, product.Category, labels.Gender));
        var incomeScores = store.GetSegment(new SegmentKey(SegmentDimension.Income, product.Category, labels.Income));

        var missing = new List<SegmentDimension>();
        if (ageScores is null) missing.Add(SegmentDimension.Age);
        if (genderScores is null) missing.Add(SegmentDimension.Gender);
        if (incomeScores is null) missing.Add(SegmentDimension.Income);

        if (missing.Count == 3)
        {
            return new RecommendationResult.NotFound(
                $"No segment data for category '{product.Category}' and the given age, gender and income groups.");
        }

        var topFeatures = store.Features
            .Where(f => product.FeatureNames.Contains(f.Name))
            .Select(f =>
            {
                var priorities = new DimensionValues(
                    Value(ageScores?.Priorities, f.Name),
                    Value(genderScores?.Priorities, f.Name),
                    Value(incomeScores?.Priorities, f.Name));
                var differences = new DimensionValues(
                    Value(ageScores?.Differences, f.Name),
                    Value(genderScores?.Differences, f.Name),
                    Value(incomeScores?.Differences, f.Name));
                double?[] available = [priorities.Age, priorities.Gender, priorities.Income];
                var known = available.OfType<double>().ToList();
                return (Feature: f, Priorities: priorities, Differences: differences,
                    Average: known.Count > 0 ? known.Average() : (double?)null);
            })
            .Where(x => x.Average is not null)
            .OrderByDescending(x => x.Average)
            .Take(request.TopN)
            .Select(x => new FeatureRecommendation(
                x.Feature.Id,
                x.Feature.Name,
                x.Feature.Definition,
                Math.Round(x.Average!.Value, 1),
                x.Priorities,
                x.Differences))
            .ToList();

        return new RecommendationResult.Success(new RecommendationResponse(
            new ProductDto(product.Asin, product.Name, product.Category),
            labels,
            missing,
            topFeatures));
    }

    private static Dictionary<string, string[]> Validate(RecommendationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Age < SegmentLabels.MinAge)
        {
            errors[nameof(request.Age)] = [$"Age must be at least {SegmentLabels.MinAge}."];
        }

        if (request.TopN is < MinTopN or > MaxTopN)
        {
            errors[nameof(request.TopN)] = [$"TopN must be between {MinTopN} and {MaxTopN}."];
        }

        if (string.IsNullOrWhiteSpace(request.Asin))
        {
            errors[nameof(request.Asin)] = ["ASIN is required."];
        }

        if (!Enum.IsDefined(request.Gender))
        {
            errors[nameof(request.Gender)] = ["Unknown gender."];
        }

        if (!Enum.IsDefined(request.Income))
        {
            errors[nameof(request.Income)] = ["Unknown income bracket."];
        }

        return errors;
    }

    private static double? Value(IReadOnlyDictionary<string, double?>? values, string feature) =>
        values is not null && values.TryGetValue(feature, out var value) ? value : null;
}
