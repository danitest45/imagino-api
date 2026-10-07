namespace Imagino.Api.Services.Generation;

public static class GenerationLifecycle
{
    // Gemini API retirement schedule, verified 2026-10-02. Cloud GA endpoints
    // have a separate lifecycle and must be integrated as a new provider/version.
    public static DateTime? RetirementAt(GenerationModel model) => model.ProviderModel switch
    {
        "veo-3.1-lite-generate-preview" or "veo-3.1-generate-preview" or "veo-3.1-fast-generate-preview"
            => new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc),
        _ => null
    };

    public static bool IsRetired(GenerationModel model, DateTime now) => RetirementAt(model) is { } date && now >= date;
    public static bool RequiresMigration(GenerationModel model, DateTime now) =>
        model.Lifecycle == "COMPATIBILITY" || RetirementAt(model) is { } date && now >= date.AddDays(-30);
}
