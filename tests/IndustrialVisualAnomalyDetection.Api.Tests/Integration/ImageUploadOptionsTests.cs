using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

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

    [Theory]
    [InlineData("ImageUpload:MaxFileSizeBytes", "0")]
    [InlineData("ImageUpload:MaxRequestBodySizeBytes", "10485760")]
    [InlineData("ImageUpload:AllowedContentTypes:0", "image/gif")]
    public void InvalidImageUploadConfigurationIsRejected(
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
                .GetRequiredService<IOptions<ImageUploadOptions>>()
                .Value;
        });
    }
}
