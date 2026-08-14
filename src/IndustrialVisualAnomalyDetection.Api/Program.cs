using IndustrialVisualAnomalyDetection.Api.Options;

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
