using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class PythonInferenceOptionsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PythonInferenceOptionsTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public void PythonInferenceConfigurationIsBound()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        PythonInferenceOptions options = scope.ServiceProvider
            .GetRequiredService<IOptions<PythonInferenceOptions>>()
            .Value;

        Assert.Equal("http://localhost:8000", options.BaseUrl);
        Assert.Equal("/api/v1/predictions", options.PredictionPath);
        Assert.Equal("/api/v1/models", options.ModelCatalogPath);
        Assert.Equal("/health/live", options.HealthPath);
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("PythonInference:BaseUrl", "relative-url")]
    [InlineData("PythonInference:PredictionPath", "api/v1/predictions")]
    [InlineData("PythonInference:ModelCatalogPath", "api/v1/models")]
    [InlineData("PythonInference:HealthPath", "health/live")]
    [InlineData("PythonInference:TimeoutSeconds", "0")]
    public void InvalidPythonInferenceConfigurationIsRejected(
        string configurationKey,
        string configurationValue)
    {
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [configurationKey] = configurationValue
                });
            });
        });

        Assert.Throws<OptionsValidationException>(() =>
        {
            using IServiceScope scope = factory.Services.CreateScope();

            _ = scope.ServiceProvider
                .GetRequiredService<IOptions<PythonInferenceOptions>>()
                .Value;
        });
    }
}
