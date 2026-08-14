using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class ImageUploadOptionsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ImageUploadOptionsTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public void ImageUploadConfigurationIsBound()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        ImageUploadOptions imageOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<ImageUploadOptions>>()
            .Value;

        FormOptions formOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<FormOptions>>()
            .Value;

        Assert.Equal(10 * 1024 * 1024, imageOptions.MaxFileSizeBytes);
        Assert.Equal(11 * 1024 * 1024, imageOptions.MaxRequestBodySizeBytes);
        Assert.Equal(["image/png", "image/jpeg"], imageOptions.AllowedContentTypes);
        Assert.Equal(imageOptions.MaxRequestBodySizeBytes, formOptions.MultipartBodyLengthLimit);
    }
}
