using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class ImageUploadOptionsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ImageUploadOptionsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ImageUploadConfigurationIsBound()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ImageUploadOptions options = scope.ServiceProvider.GetRequiredService<IOptions<ImageUploadOptions>>().Value;

        Assert.Equal(10 * 1024 * 1024, options.MaxFileSizeBytes);
        Assert.Equal(["image/png", "image/jpeg"], options.AllowedContentTypes);
    }
}
