using System.Security.Cryptography;
using Imagino.Api.Errors;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Services.Generation;

public static class OpenAiHomologationPolicy
{
    public const string RunId = "openai-three-calls-20261006";
    public const string Flare = "gpt-image-2.5-flare-2026-09-08";
    public const string Sunburst = "gpt-image-2.5-sunburst-2026-09-08";
    public const string Version = "2026-10-06.1";
    public const decimal BudgetUsd = 0.50m;
    public static readonly DateTime ExpiresAtUtc = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
    // No credible full request maximum exists in the consulted official contract. Never
    // promote the output-only estimate or an operator-supplied env number into this bound.
    public static decimal? VerifiedMaximumUsd(int call) => null;
    public static bool CostBoundsVerified => Enumerable.Range(1, 3).All(c => VerifiedMaximumUsd(c) is > 0);
    public static bool AllowsModel(GenerationModel model) => model.Provider == "openai" && model.MediaType == "image" &&
        model.Version == Version && model.Pricing.Revision == OpenAiImagePricing.Revision && (model.Id, model.ProviderModel) is
        ("openai-fast-20261006", Flare) or ("openai-studio-20261006", Sunburst);
    public static void ValidateRequest(string owner, GenerationModel model, ValidatedGeneration input)
    {
        var call = input.Prompt == BflHomologationPolicy.FastPrompt ? 1 : input.Prompt == BflHomologationPolicy.StudioPrompt ? 2 : 3;
        ValidateJob(new() { UserId = owner, Model = model, Prompt = input.Prompt, Settings = input.Settings, Inputs = input.Inputs,
            IdempotencyKey = $"openai-homologation-20261006-call-{call}", RequestHash = GenerationPolicy.Fingerprint(model, input) });
    }
    public static int ValidateJob(GenerationJob job)
    {
        if (job.UserId != BflHomologationPolicy.OwnerId || DateTime.UtcNow >= ExpiresAtUtc || !AllowsModel(job.Model) ||
            job.Settings.Count != 3 || job.Settings.GetValueOrDefault("size") != "1024x1024" ||
            job.Settings.GetValueOrDefault("quality") != "medium" || job.Settings.GetValueOrDefault("outputFormat") != "png") throw Denied();
        var call = (job.Model.ProviderModel, job.Prompt, job.Inputs.Count) switch {
            (Flare, BflHomologationPolicy.FastPrompt, 0) => 1,
            (Sunburst, BflHomologationPolicy.StudioPrompt, 0) => 2,
            (Sunburst, BflHomologationPolicy.ReferencePrompt, 1) => 3,
            _ => throw Denied()
        };
        if (call == 3 && (job.Inputs[0].Role != "reference" || Convert.ToHexString(SHA256.HashData(
            GenerationPolicy.ValidatePngInput(job.Inputs[0].Data))).ToLowerInvariant() != BflHomologationPolicy.ReferenceSha256)) throw Denied();
        if (job.IdempotencyKey != $"openai-homologation-20261006-call-{call}" ||
            job.RequestHash != GenerationPolicy.Fingerprint(job.Model, new(job.Prompt, job.Settings, job.Inputs))) throw Denied();
        return call;
    }
    public static bool CanReserve(OpenAiHomologationLedger ledger, int call, decimal? maximumUsd, DateTime now) =>
        call is >= 1 and <= 3 && maximumUsd is > 0 && ledger.Id == RunId && ledger.OwnerId == BflHomologationPolicy.OwnerId &&
        !ledger.Halted && now < ledger.ExpiresAtUtc && ledger.BudgetUsd == BudgetUsd && ledger.CommittedUsd >= 0 &&
        ledger.CommittedUsd + maximumUsd <= BudgetUsd && ledger.Calls.Count == 3 &&
        ledger.Calls.TryGetValue(call.ToString(), out var slot) && slot.State == "Available" &&
        (call == 1 || ledger.Calls.TryGetValue((call - 1).ToString(), out var previous) && previous.State == "Completed" && previous.Reconciled);
    private static ForbiddenFeatureException Denied() => new("Request is outside the finite OpenAI staging authorization.");
}
[BsonIgnoreExtraElements]
public sealed class OpenAiHomologationLedger
{
    [BsonId] public string Id { get; set; } = OpenAiHomologationPolicy.RunId;
    public string OwnerId { get; set; } = BflHomologationPolicy.OwnerId;
    public DateTime ExpiresAtUtc { get; set; } = OpenAiHomologationPolicy.ExpiresAtUtc;
    [BsonRepresentation(BsonType.Decimal128)] public decimal BudgetUsd { get; set; } = OpenAiHomologationPolicy.BudgetUsd;
    [BsonRepresentation(BsonType.Decimal128)] public decimal CommittedUsd { get; set; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal ObservedUsd { get; set; }
    public bool Halted { get; set; }
    public string? HaltReason { get; set; }
    public Dictionary<string, OpenAiHomologationSlot> Calls { get; set; } = Enumerable.Range(1, 3).ToDictionary(c => c.ToString(), _ => new OpenAiHomologationSlot());
}
public sealed class OpenAiHomologationSlot
{
    public string State { get; set; } = "Available";
    public string? JobId { get; set; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal MaximumUsd { get; set; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal? ObservedUsd { get; set; }
    public GenerationUsage? Usage { get; set; }
    public bool Reconciled { get; set; }
    public DateTime? AttemptedAtUtc { get; set; }
    public DateTime? ResponseAtUtc { get; set; }
}
