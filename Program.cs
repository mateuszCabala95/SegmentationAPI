using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using SegmentationApi.Data;
using SegmentationApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(sp =>
{
    var options = builder.Configuration.GetSection(ExcelDataOptions.SectionName).Get<ExcelDataOptions>()
        ?? throw new InvalidOperationException($"Missing '{ExcelDataOptions.SectionName}' configuration section.");

    var store = ExcelDataLoader.Load(
        Path.Combine(AppContext.BaseDirectory, options.CustomerAnalysisPath),
        Path.Combine(AppContext.BaseDirectory, options.ProductCatalogPath));

    sp.GetRequiredService<ILogger<DataStore>>().LogInformation(
        "Loaded {ProductCount} products and {FeatureCount} features from Excel.",
        store.Products.Count, store.Features.Count);
    return store;
});
builder.Services.AddSingleton<RecommendationService>();

var app = builder.Build();

// Load the Excel files at startup instead of on the first request.
app.Services.GetRequiredService<DataStore>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

var api = app.MapGroup("/api");

api.MapGet("/products", (string? category, RecommendationService service) =>
        TypedResults.Ok(service.GetProducts(category)))
    .WithName("GetProducts")
    .WithSummary("Returns all available products, unique by ASIN and category.");

api.MapPost("/recommendations", (RecommendationRequest request, RecommendationService service) =>
        service.Recommend(request) switch
        {
            RecommendationResult.Success s => Results.Ok(s.Response),
            RecommendationResult.ValidationFailed v => Results.ValidationProblem(v.Errors),
            RecommendationResult.NotFound n => Results.Problem(n.Message, statusCode: StatusCodes.Status404NotFound),
            _ => Results.Problem()
        })
    .WithName("GetRecommendations")
    .WithSummary("Returns the top N product features for the customer segment (age, gender, income).")
    .Produces<RecommendationResponse>()
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound);

app.Run();
