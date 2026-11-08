using System.Globalization;
using ClosedXML.Excel;
using SegmentationApi.Domain;

namespace SegmentationApi.Data;

/// <summary>
/// Reads customer_feature_analysis.xlsx and product_catalog.xlsx. Tables are located by their header
/// cells ("Kategoria", "ASIN", "feature_id") in column B rather than by fixed row numbers.
/// </summary>
public static class ExcelDataLoader
{
    private const int KeyColumn = 2;
    private const string FeatureDictionarySheet = "Słownik cech";
    private const string FeatureMarker = "TAK";

    private static readonly Dictionary<string, SegmentDimension> DemographicSheets = new()
    {
        ["Wiek"] = SegmentDimension.Age,
        ["Płeć"] = SegmentDimension.Gender,
        ["Dochód"] = SegmentDimension.Income
    };

    public static DataStore Load(string customerAnalysisPath, string productCatalogPath)
    {
        using var customerWorkbook = new XLWorkbook(customerAnalysisPath);
        using var catalogWorkbook = new XLWorkbook(productCatalogPath);

        var features = LoadFeatures(customerWorkbook.Worksheet(FeatureDictionarySheet));
        var featureNames = features.Select(f => f.Name).ToHashSet();

        var segments = new Dictionary<SegmentKey, SegmentScores>();
        foreach (var (sheetName, dimension) in DemographicSheets)
        {
            LoadSegments(customerWorkbook.Worksheet(sheetName), dimension, featureNames, segments);
        }

        var products = catalogWorkbook.Worksheets
            .SelectMany(ws => LoadProducts(ws, featureNames))
            .ToList();

        return new DataStore(features, products, segments);
    }

    private static List<FeatureDefinition> LoadFeatures(IXLWorksheet sheet)
    {
        var header = FindRows(sheet, "feature_id").Single();
        var columns = ReadHeader(header);

        return sheet.RowsUsed()
            .Where(r => r.RowNumber() > header.RowNumber())
            .Select(r => new FeatureDefinition(
                Text(r.Cell(KeyColumn)),
                Text(r.Cell(columns["Nazwa w Excelu"])),
                Text(r.Cell(columns["Definicja"]))))
            .Where(f => f.Id.Length > 0 && f.Name.Length > 0)
            .ToList();
    }

    private static void LoadSegments(
        IXLWorksheet sheet,
        SegmentDimension dimension,
        IReadOnlySet<string> featureNames,
        Dictionary<SegmentKey, SegmentScores> segments)
    {
        // First "Kategoria" table holds priorities (0-100), the second one differences (pp).
        var headers = FindRows(sheet, "Kategoria").ToList();
        if (headers.Count != 2)
        {
            throw new InvalidDataException($"Sheet '{sheet.Name}' should contain 2 tables, found {headers.Count}.");
        }

        var priorities = ReadSegmentTable(sheet, headers[0], featureNames);
        var differences = ReadSegmentTable(sheet, headers[1], featureNames);

        foreach (var ((category, group), (customerCount, values)) in priorities)
        {
            var diff = differences.TryGetValue((category, group), out var d)
                ? d.Values
                : new Dictionary<string, double?>();

            segments[new SegmentKey(dimension, category, group)] = new SegmentScores(customerCount, values, diff);
        }
    }

    private static Dictionary<(string Category, string Group), (int CustomerCount, Dictionary<string, double?> Values)>
        ReadSegmentTable(IXLWorksheet sheet, IXLRow header, IReadOnlySet<string> featureNames)
    {
        var columns = ReadHeader(header);
        var groupColumn = KeyColumn + 1;
        var countColumn = columns["N klientów"];
        var featureColumns = columns.Where(c => featureNames.Contains(c.Key)).ToList();

        var result = new Dictionary<(string, string), (int, Dictionary<string, double?>)>();
        for (var rowNumber = header.RowNumber() + 1; ; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            var category = Text(row.Cell(KeyColumn));
            var group = Text(row.Cell(groupColumn));
            if (category.Length == 0 || group.Length == 0)
            {
                break;
            }

            var values = featureColumns.ToDictionary(c => c.Key, c => Number(row.Cell(c.Value)));
            result[(category, group)] = ((int)(Number(row.Cell(countColumn)) ?? 0), values);
        }

        return result;
    }

    private static IEnumerable<Product> LoadProducts(IXLWorksheet sheet, IReadOnlySet<string> featureNames)
    {
        var header = FindRows(sheet, "ASIN").FirstOrDefault();
        if (header is null)
        {
            yield break;
        }

        // Cell B2 holds the full category name, which differs from the sheet name for some sheets
        // (e.g. DRIVE_OR_STORAGE -> COMPUTER_DRIVE_OR_STORAGE).
        var title = Text(sheet.Cell(2, KeyColumn));
        var category = title.Length > 0 ? title : sheet.Name;

        var columns = ReadHeader(header);
        var nameColumn = columns["Nazwa produktu"];
        var featureColumns = columns.Where(c => featureNames.Contains(c.Key)).ToList();

        foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
        {
            var asin = Text(row.Cell(KeyColumn));
            if (asin.Length == 0)
            {
                continue;
            }

            var productFeatures = featureColumns
                .Where(c => Text(row.Cell(c.Value)).Equals(FeatureMarker, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Key)
                .ToHashSet();

            yield return new Product(asin, Text(row.Cell(nameColumn)), category, sheet.Name, productFeatures);
        }
    }

    private static IEnumerable<IXLRow> FindRows(IXLWorksheet sheet, string keyCellValue) =>
        sheet.RowsUsed().Where(r => Text(r.Cell(KeyColumn)) == keyCellValue);

    private static Dictionary<string, int> ReadHeader(IXLRow header) =>
        header.CellsUsed()
            .Select(c => (Name: Text(c), Column: c.Address.ColumnNumber))
            .Where(c => c.Name.Length > 0)
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => g.First().Column);

    private static string Text(IXLCell cell) => cell.GetFormattedString().Trim();

    private static double? Number(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.DataType == XLDataType.Number)
        {
            return cell.GetDouble();
        }

        return double.TryParse(Text(cell), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
