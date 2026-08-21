using IndustrialVisualAnomalyDetection.Api.Application.Models;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Unit;

public sealed class InferenceModelCatalogTests
{
    [Fact]
    public void ValidCatalogPreservesModels()
    {
        InferenceModelDescriptor defaultModel = new(
            "capsule-320",
            "MVTec AD - Capsule",
            "capsule",
            320,
            true);

        InferenceModelDescriptor additionalModel = new(
            "cashew-q95-320",
            "VisA - Cashew",
            "cashew",
            320,
            false);

        InferenceModelCatalog catalog = new(
            "capsule-320",
            [defaultModel, additionalModel]);

        Assert.Equal("capsule-320", catalog.DefaultModelId);
        Assert.Equal(2, catalog.Models.Count);
        Assert.Same(defaultModel, catalog.Models[0]);
        Assert.Same(additionalModel, catalog.Models[1]);
    }

    [Fact]
    public void EmptyCatalogIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new InferenceModelCatalog(
                "capsule-320",
                []));
    }

    [Fact]
    public void DuplicateModelIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new InferenceModelCatalog(
                "capsule-320",
                [
                    new InferenceModelDescriptor(
                        "capsule-320",
                        "First",
                        "capsule",
                        320,
                        true),
                    new InferenceModelDescriptor(
                        "capsule-320",
                        "Second",
                        "capsule",
                        320,
                        false)
                ]));
    }

    [Fact]
    public void MismatchedDefaultModelIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new InferenceModelCatalog(
                "cashew-q95-320",
                [
                    new InferenceModelDescriptor(
                        "capsule-320",
                        "Capsule",
                        "capsule",
                        320,
                        true),
                    new InferenceModelDescriptor(
                        "cashew-q95-320",
                        "Cashew",
                        "cashew",
                        320,
                        false)
                ]));
    }

    [Fact]
    public void NonPositiveInputSizeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InferenceModelDescriptor(
                "capsule-320",
                "Capsule",
                "capsule",
                0,
                true));
    }
}
