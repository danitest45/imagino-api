using System.Buffers.Binary;
using System.Security.Cryptography;
using Imagino.Api.Errors;
using MongoDB.Bson.Serialization.Attributes;

namespace Imagino.Api.Services.Generation;

// One finite staging authorization. Changing an env var or restarting cannot reset its ledger.
public static class BflHomologationPolicy
{
    public const string RunId = "bfl-three-calls-20261005";
    public const string OwnerId = "6ac038cb05509cea703277eb";
    public const string ServiceId = "srv-db1tmv17lnhs73efdjp0";
    public const decimal BudgetUsd = 0.15m;
    public static readonly DateTime ExpiresAtUtc = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
    public const string ReferenceSha256 = "f528f7cdc43f68d75f27e3fd3cbc491a02bb296a6f73c5fe57b374834517b333";
    public const string FastPrompt = "A premium product photograph of a translucent cobalt blue glass perfume bottle on a warm beige stone pedestal, soft studio lighting, subtle realistic shadow, minimal luxury advertising composition, no text, 1:1.";
    public const string StudioPrompt = "A cinematic editorial photograph of a vintage red sports car parked outside a modern concrete house at blue hour, wet pavement reflections, realistic materials, sophisticated architectural photography, no people, no text, 1:1.";
    public const string ReferencePrompt = "Preserve the exact cobalt blue perfume bottle from the reference image, including its rounded rectangular silhouette, short blue cap, narrow gold neck band, and three small gold circles arranged in a triangle on the lower front. Place the same product on a dark slate pedestal in a lush botanical conservatory with softly blurred green foliage, warm morning light, realistic materials and shadows, premium product photography, no text, no people, 1:1.";
    public static bool AllowsModel(GenerationModel model) => model.Provider == "bfl" && model.MediaType == "image" &&
        model.Version == "2026-10-02.1" && (model.Id, model.ProviderModel) is
        ("flux-fast-20261002", "flux-2-klein-4b") or ("flux-studio-20261002", "flux-2-pro");
    public static decimal Cost(int call) => call switch { 1 => 0.014m, 2 => 0.030m, 3 => 0.045m, _ => throw Denied() };
    public static int Credits(int call) => call switch { 1 => 5, 2 => 10, 3 => 15, _ => throw Denied() };
    public static int Validate(string owner, GenerationModel model, ValidatedGeneration input, GenerationQuote quote, DateTime now)
    {
        if (owner != OwnerId || now >= ExpiresAtUtc || !AllowsModel(model) ||
            input.Settings.Count != 2 || input.Settings.GetValueOrDefault("resolution") != "1MP" ||
            input.Settings.GetValueOrDefault("aspectRatio") != "1:1") throw Denied();
        var call = (model.ProviderModel, input.Prompt, input.Inputs.Count) switch {
            ("flux-2-klein-4b", FastPrompt, 0) => 1,
            ("flux-2-pro", StudioPrompt, 0) => 2,
            ("flux-2-pro", ReferencePrompt, 1) => 3,
            _ => throw Denied()
        };
        if (call == 3 && (input.Inputs[0].Role != "reference" ||
            Convert.ToHexString(SHA256.HashData(GenerationPolicy.ValidatePngInput(input.Inputs[0].Data))).ToLowerInvariant() != ReferenceSha256))
            throw Denied();
        if (quote.ProviderCostEstimateUsd != Cost(call) || quote.Credits != Credits(call)) throw Denied();
        return call;
    }
    public static int ValidateJob(GenerationJob job)
    {
        var input = new ValidatedGeneration(job.Prompt, job.Settings, job.Inputs);
        var call = Validate(job.UserId, job.Model, input, job.Quote, DateTime.UtcNow);
        if (job.IdempotencyKey != $"bfl-homologation-20261005-call-{call}" ||
            job.RequestHash != GenerationPolicy.Fingerprint(job.Model, input)) throw Denied();
        return call;
    }
    public static (int Width, int Height) ValidateOutput(byte[] bytes)
    {
        if (bytes.Length < 33 || bytes.Length > Imagino.Api.Security.GeneratedImageValidator.MaxBytes ||
            !bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)) throw new InvalidDataException("Expected a 1MP PNG output.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width != 1024 || height != 1024) throw new InvalidDataException("Expected a 1024 by 1024 output.");
        return ((int)width, (int)height);
    }
    private static ForbiddenFeatureException Denied() => new("Request is outside the finite BFL staging authorization.");
}

[BsonIgnoreExtraElements]
public sealed class BflHomologationLedger
{
    [BsonId] public string Id { get; set; } = BflHomologationPolicy.RunId;
    public string OwnerId { get; set; } = BflHomologationPolicy.OwnerId;
    public decimal BudgetUsd { get; set; } = BflHomologationPolicy.BudgetUsd;
    public decimal CommittedUsd { get; set; }
    public bool Halted { get; set; }
    public string? HaltReason { get; set; }
    public DateTime ExpiresAtUtc { get; set; } = BflHomologationPolicy.ExpiresAtUtc;
    public Dictionary<string, BflHomologationCall> Calls { get; set; } = Enumerable.Range(1, 3)
        .ToDictionary(n => n.ToString(), n => new BflHomologationCall { EstimatedUsd = BflHomologationPolicy.Cost(n) });
}
[BsonIgnoreExtraElements]
public sealed class BflHomologationCall
{
    public string State { get; set; } = "Available";
    public string? JobId { get; set; }
    public decimal EstimatedUsd { get; set; }
    public decimal? ReportedUsd { get; set; }
    public bool Reconciled { get; set; }
    public DateTime? AttemptedAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? SettledAtUtc { get; set; }
}
