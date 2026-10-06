namespace Imagino.Api.Services.Generation;

public static class GenerationCatalog
{
    public const string Revision = "2026-10-06.1";
    public static List<GenerationModel> Seed(bool fixture)
    {
        var aspect = new GenerationField("aspectRatio", "Aspect ratio", "enum", "1:1", new[] { "1:1", "16:9", "9:16" });
        var mp = new GenerationField("resolution", "Output size", "enum", "1MP", new[] { "1MP", "4MP" });
        var list = new List<GenerationModel> {
            new() { Id = "flux-fast-20261002", DisplayName = "Fast Image", Category = "Explore", SortOrder = 10,
                Description = "Quick concepts and variations. FLUX.2 klein 4B.", Provider = "bfl", ProviderModel = "flux-2-klein-4b",
                Capabilities = new[] { "textToImage" }, Fields = new() { aspect, mp },
                Pricing = new() { Unit = "megapixel", Source = "https://bfl.ai/pricing?category=flux.2", RatesUsd = new() { ["1MP"] = 0.014m, ["4MP"] = 0.014m }, ExtraMegapixelUsd = 0.001m } },
            new() { Id = "flux-studio-20261002", DisplayName = "Studio Image", Category = "Create", SortOrder = 20,
                Description = "Production images and product or character references. FLUX.2 pro, fixed endpoint.", Provider = "bfl", ProviderModel = "flux-2-pro",
                Capabilities = new[] { "textToImage", "imageEditing", "referenceImages" }, Fields = new() { aspect, mp },
                Inputs = new() { new("reference", "Reference images", 4) },
                Pricing = new() { Unit = "megapixel", Source = "https://bfl.ai/pricing?category=flux.2", RatesUsd = new() { ["1MP"] = 0.03m, ["4MP"] = 0.03m }, ExtraMegapixelUsd = 0.015m, ReferenceUsd = 0.015m } },
            new() { Id = "gemini-edit-20261002", DisplayName = "Edit & Design", Category = "Refine", SortOrder = 30,
                Description = "Natural-language edits, references and layouts. Gemini 3.1 Flash Image.", Provider = "gemini-image", ProviderModel = "gemini-3.1-flash-image", TimeoutSeconds = 600,
                Capabilities = new[] { "textToImage", "imageEditing", "referenceImages" },
                Fields = new() { aspect, new("resolution", "Resolution", "enum", "1K", new[] { "1K", "2K", "4K" }) },
                Inputs = new() { new("reference", "Reference images", 4) },
                Pricing = new() { Source = "https://ai.google.dev/gemini-api/docs/pricing", RatesUsd = new() { ["1K"] = 0.067m, ["2K"] = 0.101m, ["4K"] = 0.151m }, ReferenceUsd = 0.002m, OverheadUsd = 0.007m } },
            Video("veo-fast-20261002", "Fast Video", "Short clips with native audio. Veo 3.1 Lite.", "veo-3.1-lite-generate-preview", 0.05m, 0.08m, 40),
            Video("veo-cinema-20261002", "Cinema Video", "Cinematic clips with native audio. Veo 3.1.", "veo-3.1-generate-preview", 0.40m, 0.40m, 50)
        };
        list.AddRange(new[] {
            OpenAi("openai-fast-20261006", "OpenAI Fast (experimental)", OpenAiHomologationPolicy.Flare, false, 15),
            OpenAi("openai-studio-20261006", "OpenAI Studio & Edit (experimental)", OpenAiHomologationPolicy.Sunburst, true, 25)
        });
        if (fixture) list.Add(new() { Id = "pipeline-demo-20261002", DisplayName = "Pipeline Demo", Category = "Staging", SortOrder = 90,
            Description = "Synthetic test image. Exercises jobs, credits and storage; no AI model is called.", Provider = "fixture", ProviderModel = "synthetic-png-v1",
            Availability = "synthetic_demo", Capabilities = new[] { "textToImage" },
            Fields = new() { new("resolution", "Output size", "enum", "1MP", new[] { "1MP" }),
                new("outcome", "Test outcome", "enum", "success", new[] { "success", "failure" }) },
            Pricing = new() { Source = "synthetic staging fixture", RatesUsd = new() { ["1MP"] = 0m }, RiskMultiplier = 1m, OverheadUsd = 0m } });
        return list;
    }
    private static GenerationModel OpenAi(string id, string name, string snapshot, bool edit, int order) => new() {
        Id = id, DisplayName = name, Category = edit ? "Create" : "Explore", Provider = "openai", ProviderModel = snapshot,
        Version = OpenAiHomologationPolicy.Version, SortOrder = order,
        Description = "Experimental staging benchmark. Starting credits estimate output only; text/reference inputs are additional. Financial authorization required; observed token usage determines cost.",
        Capabilities = edit ? new[] { "textToImage", "imageEditing", "referenceImages" } : new[] { "textToImage" },
        Fields = new() { new("size", "Output size", "enum", "1024x1024", new[] { "1024x1024" }),
            new("quality", "Quality", "enum", "medium", new[] { "medium" }),
            new("outputFormat", "Output format", "enum", "png", new[] { "png" }) },
        Inputs = edit ? new() { new("reference", "Controlled reference", 1) } : new(),
        Pricing = new() { Revision = OpenAiImagePricing.Revision, Unit = "token", Source = "https://developers.openai.com/api/docs/pricing",
            RatesUsd = new() { ["text_input_per_million"] = OpenAiImagePricing.TextInputPerMillion,
                ["image_input_per_million"] = OpenAiImagePricing.ImageInputPerMillion, ["image_output_per_million"] = OpenAiImagePricing.ImageOutputPerMillion } }
    };
    private static GenerationModel Video(string id, string name, string description, string providerModel, decimal hd, decimal fullHd, int order) =>
        new() { Id = id, DisplayName = name, Category = "Motion", MediaType = "video", Provider = "google-veo", ProviderModel = providerModel,
            Description = description + " Compatibility preview; endpoint retires October 22. Migration required before activation.", Lifecycle = "COMPATIBILITY",
            Version = "2026-10-02.2", SortOrder = order, TimeoutSeconds = 1200,
            Capabilities = new[] { "textToVideo", "imageToVideo", "firstFrame", "lastFrame", "nativeAudio" },
            Fields = new() { new("aspectRatio", "Aspect ratio", "enum", "16:9", new[] { "16:9", "9:16" }),
                new("resolution", "Resolution", "enum", "720p", new[] { "720p", "1080p" }),
                new("duration", "Duration (seconds)", "integer", "4", new[] { "4", "6", "8" }) },
            Inputs = new() { new("firstFrame", "First frame", 1), new("lastFrame", "Last frame", 1) },
            Rules = new() { new("resolution", "1080p", "duration", new[] { "8" }) },
            Pricing = new() { Unit = "second", Source = "https://ai.google.dev/gemini-api/docs/pricing", RatesUsd = new() { ["720p"] = hd, ["1080p"] = fullHd }, OverheadUsd = 0.01m } };
}
