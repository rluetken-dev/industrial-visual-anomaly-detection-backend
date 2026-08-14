using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class OpenApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiContractTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task AnalysisEndpointExposesMultipartImageContract()
    {
        using HttpClient client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using Stream content = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(content);

        JsonElement operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/analyses")
            .GetProperty("post");

        Assert.Equal("AnalyzeImage", operation.GetProperty("operationId").GetString());
        Assert.Equal(
            "Analyze an industrial image",
            operation.GetProperty("summary").GetString());

        JsonElement imageSchema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("multipart/form-data")
            .GetProperty("schema")
            .GetProperty("properties")
            .GetProperty("image");

        Assert.Equal(
            "#/components/schemas/IFormFile",
            imageSchema.GetProperty("$ref").GetString());

        JsonElement fileSchema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("IFormFile");

        Assert.Equal("string", fileSchema.GetProperty("type").GetString());
        Assert.Equal("binary", fileSchema.GetProperty("format").GetString());
    }
}
