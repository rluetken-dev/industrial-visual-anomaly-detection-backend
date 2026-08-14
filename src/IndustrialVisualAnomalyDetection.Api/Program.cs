using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using IndustrialVisualAnomalyDetection.Api.Errors;
using IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;
using IndustrialVisualAnomalyDetection.Api.Options;
using IndustrialVisualAnomalyDetection.Api.Validation.Images;
using IndustrialVisualAnomalyDetection.Api.Application.Health;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddOptions<ImageUploadOptions>()
    .BindConfiguration(ImageUploadOptions.SectionName)
    .Validate(options => options.MaxFileSizeBytes > 0, "The maximum image file size must be greater than zero.")
    .Validate(options => options.AllowedContentTypes.Length > 0, "At least one image content type must be allowed.")
    .ValidateOnStart();

builder.Services.AddScoped<IImageUploadValidator, ImageUploadValidator>();

builder.Services.AddHttpClient<IAnomalyAnalyzer, PythonServiceAnomalyAnalyzer>(
    (serviceProvider, httpClient) =>
    {
        PythonInferenceOptions options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<PythonInferenceOptions>>()
            .Value;

        httpClient.BaseAddress = new Uri(options.BaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

builder.Services.AddHttpClient<IInferenceServiceHealthProbe, PythonInferenceServiceHealthProbe>(
    (serviceProvider, httpClient) =>
    {
        PythonInferenceOptions options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<PythonInferenceOptions>>()
            .Value;

        httpClient.BaseAddress = new Uri(options.BaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

builder.Services.AddExceptionHandler<InvalidImageContentExceptionHandler>();
builder.Services.AddExceptionHandler<InferenceUnavailableExceptionHandler>();
builder.Services.AddExceptionHandler<InferenceUnavailableExceptionHandler>();

builder.Services.AddOptions<PythonInferenceOptions>()
    .BindConfiguration(PythonInferenceOptions.SectionName)
    .Validate(options =>
    {
        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseUri)
            && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps);
    }, "The Python inference base URL must be an absolute HTTP or HTTPS URL.")
    .Validate(options => Uri.TryCreate(options.PredictionPath, UriKind.Relative, out _),
        "The Python inference prediction path must be relative.")
    .Validate(options => Uri.TryCreate(options.HealthPath, UriKind.Relative, out _),
        "The Python inference health path must be relative.")
    .Validate(options => options.TimeoutSeconds > 0,
        "The Python inference timeout must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
