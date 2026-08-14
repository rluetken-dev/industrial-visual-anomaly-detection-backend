using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Tests;

public sealed class PythonInferenceOptionsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PythonInferenceOptionsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void PythonInferenceConfigurationIsBound()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        PythonInferenceOptions options =
            scope.ServiceProvider.GetRequiredService<IOptions<PythonInferenceOptions>>().Value;

        Assert.Equal("http://localhost:8000", options.BaseUrl);
        Assert.Equal("/api/v1/predictions", options.PredictionPath);
        Assert.Equal("/health/live", options.HealthPath);
        Assert.Equal(30, options.TimeoutSeconds);
    }
}
