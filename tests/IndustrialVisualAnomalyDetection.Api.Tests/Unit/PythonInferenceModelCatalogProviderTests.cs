using IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;
using IndustrialVisualAnomalyDetection.Api.Options;
using System.Net;
using System.Net.Http.Json;
using IndustrialVisualAnomalyDetection.Api.Application.Models;
using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using System.Text.Json;

namespace IndustrialVisualAnomalyDetection.Api.Tests;

public sealed class PythonInferenceModelCatalogProviderTests
{
    [Fact]
    public void NullHttpClientIsRejected()
    {
        PythonInferenceOptions options = new();

        Assert.Throws<ArgumentNullException>(() =>
            new PythonInferenceModelCatalogProvider(
                null!,
                Microsoft.Extensions.Options.Options.Create(options)));
    }

    [Fact]
    public void NullOptionsAreRejected()
    {
        using HttpClient httpClient = new();

        Assert.Throws<ArgumentNullException>(() =>
            new PythonInferenceModelCatalogProvider(httpClient, null!));
    }

    [Fact]
    public async Task SuccessfulResponseIsMappedToModelCatalog()
    {
        StubHttpMessageHandler handler = new((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("http://localhost:8000/api/v1/models", request.RequestUri?.AbsoluteUri);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    defaultModelId = "capsule",
                    models = new object[]
                    {
                    new
                    {
                        id = "capsule",
                        displayName = "MVTec AD - Capsule",
                        category = "capsule",
                        inputSize = 320,
                        isDefault = true
                    },
                    new
                    {
                        id = "cashew",
                        displayName = "VisA - Cashew",
                        category = "cashew",
                        inputSize = 320,
                        isDefault = false
                    }
                    }
                })
            });
        });

        PythonInferenceModelCatalogProvider provider = CreateProvider(handler);

        InferenceModelCatalog catalog = await provider.GetCatalogAsync(CancellationToken.None);

        Assert.Equal("capsule", catalog.DefaultModelId);
        Assert.Collection(
            catalog.Models,
            model =>
            {
                Assert.Equal("capsule", model.Id);
                Assert.Equal("MVTec AD - Capsule", model.DisplayName);
                Assert.Equal("capsule", model.Category);
                Assert.Equal(320, model.InputSize);
                Assert.True(model.IsDefault);
            },
            model =>
            {
                Assert.Equal("cashew", model.Id);
                Assert.Equal("VisA - Cashew", model.DisplayName);
                Assert.Equal("cashew", model.Category);
                Assert.Equal(320, model.InputSize);
                Assert.False(model.IsDefault);
            });
    }

    [Fact]
    public async Task UnsuccessfulResponseIsMappedToUnavailableException()
    {
        StubHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        PythonInferenceModelCatalogProvider provider = CreateProvider(handler);

        await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
            provider.GetCatalogAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InvalidJsonIsMappedToUnavailableException()
    {
        StubHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{")
            }));

        PythonInferenceModelCatalogProvider provider = CreateProvider(handler);

        InferenceUnavailableException exception =
            await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
                provider.GetCatalogAsync(CancellationToken.None));

        Assert.Equal(
            "The Python inference service returned an invalid model catalog.",
            exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task InvalidModelIsMappedToUnavailableException()
    {
        StubHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    defaultModelId = "capsule",
                    models = new[]
                    {
                    new
                    {
                        id = "capsule",
                        displayName = "MVTec AD - Capsule",
                        category = "capsule",
                        inputSize = 0,
                        isDefault = true
                    }
                    }
                })
            }));

        PythonInferenceModelCatalogProvider provider = CreateProvider(handler);

        InferenceUnavailableException exception =
            await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
                provider.GetCatalogAsync(CancellationToken.None));

        Assert.Equal(
            "The Python inference service returned an invalid model catalog.",
            exception.Message);
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    private static PythonInferenceModelCatalogProvider CreateProvider(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };

        PythonInferenceOptions options = new()
        {
            BaseUrl = "http://localhost:8000",
            ModelCatalogPath = "/api/v1/models",
            TimeoutSeconds = 30
        };

        return new PythonInferenceModelCatalogProvider(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public async Task UnreachableServiceIsMappedToUnavailableException()
    {
        HttpRequestException networkException = new("Connection refused.");

        StubHttpMessageHandler handler = new((_, _) =>
            throw networkException);

        PythonInferenceModelCatalogProvider provider = CreateProvider(handler);

        InferenceUnavailableException exception =
            await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
                provider.GetCatalogAsync(CancellationToken.None));

        Assert.Equal(
            "The Python inference service could not be reached while loading the model catalog.",
            exception.Message);
        Assert.Same(networkException, exception.InnerException);
    }

    [Fact]
    public async Task TimeoutIsMappedToUnavailableException()
    {
        TaskCanceledException timeoutException = new("The request timed out.");

        StubHttpMessageHandler handler = new((_, _) =>
            throw timeoutException);

        PythonInferenceModelCatalogProvider provider = CreateProvider(handler);

        InferenceUnavailableException exception =
            await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
                provider.GetCatalogAsync(CancellationToken.None));

        Assert.Equal(
            "The Python inference model catalog request timed out.",
            exception.Message);
        Assert.Same(timeoutException, exception.InnerException);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
