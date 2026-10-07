using Imagino.Api.Errors;

namespace Imagino.Api.Services.Generation;

// Operator-approved observed-use ceiling. This is not a provider-enforced request cap.
public static class OpenAiSingleSmokePolicy
{
    public const string RunId = "openai-flare-single-smoke-20261006";
    public const string Key = "openai-flare-single-smoke-20261006-only-slot";
    public const decimal ObservedCeilingUsd = 0.10m;
    public const long PlanningTextTokens = 512;
    public static decimal ProjectionUsd => (PlanningTextTokens * OpenAiImagePricing.TextInputPerMillion +
        OpenAiImagePricing.OutputOnlyProjectedTokens * OpenAiImagePricing.ImageOutputPerMillion) / 1_000_000m;
    public static bool AllowsModel(GenerationModel model) => OpenAiHomologationPolicy.AllowsModel(model) &&
        model.Id == "openai-fast-20261006" && model.ProviderModel == OpenAiHomologationPolicy.Flare;
    public static OpenAiHomologationLedger NewLedger() => new() {
        Id = RunId, BudgetUsd = ObservedCeilingUsd,
        Calls = new() { ["1"] = new() { ProjectedUsd = ProjectionUsd } }
    };
    public static void ValidateRequest(string owner, GenerationModel model, ValidatedGeneration input) =>
        ValidateJob(new() { UserId = owner, Model = model, Prompt = input.Prompt, Settings = input.Settings,
            Inputs = input.Inputs, IdempotencyKey = Key, OpenAiRunId = RunId,
            RequestHash = GenerationPolicy.Fingerprint(model, input) });
    public static void ValidateJob(GenerationJob job)
    {
        if (job.OpenAiRunId != RunId || job.UserId != BflHomologationPolicy.OwnerId || !AllowsModel(job.Model) ||
            DateTime.UtcNow >= OpenAiHomologationPolicy.ExpiresAtUtc || job.IdempotencyKey != Key ||
            job.Prompt != BflHomologationPolicy.FastPrompt || job.Inputs.Count != 0 || job.Settings.Count != 3 ||
            job.Settings.GetValueOrDefault("size") != "1024x1024" || job.Settings.GetValueOrDefault("quality") != "medium" ||
            job.Settings.GetValueOrDefault("outputFormat") != "png" ||
            job.RequestHash != GenerationPolicy.Fingerprint(job.Model, new(job.Prompt, job.Settings, job.Inputs)) ||
            ProjectionUsd >= ObservedCeilingUsd / 2)
            throw new ForbiddenFeatureException("Request is outside the one-call Flare smoke authorization.");
    }
    public static bool CanReserve(OpenAiHomologationLedger ledger, DateTime now) =>
        ledger.Id == RunId && ledger.OwnerId == BflHomologationPolicy.OwnerId && !ledger.Halted &&
        now < ledger.ExpiresAtUtc && ledger.BudgetUsd == ObservedCeilingUsd && ledger.CommittedUsd == 0 &&
        ledger.ObservedUsd == 0 && ledger.Calls.Count == 1 && ledger.Calls.TryGetValue("1", out var slot) &&
        slot.State == "Available" && slot.JobId == null && slot.AttemptedAtUtc == null &&
        slot.MaximumUsd == 0 && slot.ProjectedUsd == ProjectionUsd && ProjectionUsd < ObservedCeilingUsd / 2;
}
