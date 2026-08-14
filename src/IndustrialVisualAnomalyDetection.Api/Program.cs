using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using IndustrialVisualAnomalyDetection.Api.Application.Health;
using IndustrialVisualAnomalyDetection.Api.Errors;
using IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;
using IndustrialVisualAnomalyDetection.Api.Options;
using IndustrialVisualAnomalyDetection.Api.Validation.Images;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

long maxRequestBodySizeBytes = builder.Configuration.GetValue<long?>(
    $"{ImageUploadOptions.SectionName}:{nameof(ImageUploadOptions.MaxRequestBodySizeBytes)}")
    ?? ImageUploadOptions.DefaultMaxRequestBodySizeBytes;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodySizeBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxRequestBodySizeBytes;
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddOptions<ImageUploadOptions>()
    .BindConfiguration(ImageUploadOptions.SectionName)
    .Validate(
        options => options.MaxFileSizeBytes > 0,
        "The maximum image file size must be greater than zero.")
    .Validate(
        options => options.MaxRequestBodySizeBytes > options.MaxFileSizeBytes,
        "The maximum request body size must be greater than the maximum image file size.")
    .Validate(
        options => options.AllowedContentTypes is { Length: > 0 },
        "At least one image content type must be allowed.")
    .Validate(
        options => options.AllowedContentTypes?.All(contentType =>
            string.Equals(
                contentType,
                "image/png",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                contentType,
                "image/jpeg",
                StringComparison.OrdinalIgnoreCase)) == true,
        "Only image/png and image/jpeg can be configured as allowed image content types.")
    .ValidateOnStart();

builder.Services.AddOptions<PythonInferenceOptions>()
    .BindConfiguration(PythonInferenceOptions.SectionName)
    .Validate(
        options =>
            Uri.TryCreate(
                options.BaseUrl,
                UriKind.Absolute,
                out Uri? baseUri)
            && (baseUri.Scheme == Uri.UriSchemeHttp
                || baseUri.Scheme == Uri.UriSchemeHttps),
        "The Python inference base URL must be an absolute HTTP or HTTPS URL.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.PredictionPath)
            && options.PredictionPath.StartsWith('/')
            && Uri.TryCreate(
                options.PredictionPath,
                UriKind.Relative,
                out _),
        "The Python inference prediction path must be a root-relative path.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.HealthPath)
            && options.HealthPath.StartsWith('/')
            && Uri.TryCreate(
                options.HealthPath,
                UriKind.Relative,
                out _),
        "The Python inference health path must be a root-relative path.")
    .Validate(
        options => options.TimeoutSeconds > 0,
        "The Python inference timeout must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddOptions<ApiCorsOptions>()
    .BindConfiguration(ApiCorsOptions.SectionName)
    .Validate(
        options => options.AllowedOrigins is not null,
        "The CORS allowed origins collection must not be null.")
    .Validate(
        options => options.AllowedOrigins?.All(origin =>
            Uri.TryCreate(
                origin,
                UriKind.Absolute,
                out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps)
            && uri.AbsolutePath == "/"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)) == true,
        "Every CORS origin must be an absolute HTTP or HTTPS origin without a path, query, or fragment.")
    .ValidateOnStart();

builder.Services.AddCors();

builder.Services
    .AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<IOptions<ApiCorsOptions>>((corsOptions, configuredOptions) =>
    {
        string[] allowedOrigins = configuredOptions.Value.AllowedOrigins;

        corsOptions.AddPolicy(ApiCorsOptions.PolicyName, policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
    });

builder.Services.AddScoped<IImageUploadValidator, ImageUploadValidator>();

builder.Services.AddHttpClient<IAnomalyAnalyzer, PythonServiceAnomalyAnalyzer>(
    (serviceProvider, httpClient) =>
    {
        PythonInferenceOptions options = serviceProvider
            .GetRequiredService<IOptions<PythonInferenceOptions>>()
            .Value;

        httpClient.BaseAddress = new Uri(options.BaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

builder.Services.AddHttpClient<
    IInferenceServiceHealthProbe,
    PythonInferenceServiceHealthProbe>(
    (serviceProvider, httpClient) =>
    {
        PythonInferenceOptions options = serviceProvider
            .GetRequiredService<IOptions<PythonInferenceOptions>>()
            .Value;

        httpClient.BaseAddress = new Uri(options.BaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

builder.Services.AddExceptionHandler<InvalidImageContentExceptionHandler>();
builder.Services.AddExceptionHandler<InferenceUnavailableExceptionHandler>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseCors(ApiCorsOptions.PolicyName);
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
