namespace SegmentationApi.Domain;

/// <summary>
/// Maps request values to the group labels used in customer_feature_analysis.xlsx.
/// </summary>
public static class SegmentLabels
{
    public const int MinAge = 18;

    public static string ForGender(Gender gender) => gender switch
    {
        Gender.Female => "Female",
        Gender.Male => "Male",
        Gender.Other => "Other",
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null)
    };

    public static string ForIncome(IncomeBracket income) => income switch
    {
        IncomeBracket.LessThan25k => "Less than $25,000",
        IncomeBracket.From25kTo49k => "$25,000 - $49,999",
        IncomeBracket.From50kTo74k => "$50,000 - $74,999",
        IncomeBracket.From75kTo99k => "$75,000 - $99,999",
        IncomeBracket.From100kTo149k => "$100,000 - $149,999",
        IncomeBracket.From150kOrMore => "$150,000 or more",
        _ => throw new ArgumentOutOfRangeException(nameof(income), income, null)
    };

    public static string ForAge(int age) => age switch
    {
        < MinAge => throw new ArgumentOutOfRangeException(nameof(age), age, $"Age must be at least {MinAge}."),
        <= 24 => "18 - 24 years",
        <= 34 => "25 - 34 years",
        <= 44 => "35 - 44 years",
        <= 54 => "45 - 54 years",
        <= 64 => "55 - 64 years",
        _ => "65 and older"
    };
}
