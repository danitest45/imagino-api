using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Imagino.Api.Errors;

namespace Imagino.Api.Services.Generation;

public static class GenerationPolicy
{
    public static ValidatedGeneration Validate(GenerationModel model, GenerationRequest request)
    {
        if (!model.Enabled || model.Lifecycle is not ("ACTIVE" or "COMPATIBILITY")) throw new ValidationAppException("Model is disabled.");
        if (GenerationLifecycle.IsRetired(model, DateTime.UtcNow)) throw new ValidationAppException("Model endpoint is retired. Migration required.");
        if (!model.ProviderEnabled) throw new ValidationAppException("Provider is disabled.");
        var prompt = request.Prompt?.Trim() ?? "";
        if (prompt.Length is < 1 or > 2000) throw new ValidationAppException("Prompt must contain 1 to 2000 characters.");
        if (request.Settings == null || request.Inputs == null) throw new ValidationAppException("Settings and inputs cannot be null.");
        if (request.Settings.Keys.Any(k => model.Fields.All(f => f.Key != k)))
            throw new ValidationAppException("Unsupported generation setting.");
        var settings = new Dictionary<string, string>();
        foreach (var field in model.Fields)
        {
            var value = field.DefaultValue;
            if (request.Settings.TryGetValue(field.Key, out var element))
            {
                if (field.Type == "integer")
                {
                    if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var n))
                        throw new ValidationAppException($"'{field.Label}' must be an integer.");
                    value = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    if (element.ValueKind != JsonValueKind.String) throw new ValidationAppException($"'{field.Label}' must be a string.");
                    value = element.GetString()!;
                }
            }
            if (!field.Options.Contains(value)) throw new ValidationAppException($"Unsupported value for '{field.Label}'.");
            settings[field.Key] = value;
        }
        foreach (var rule in model.Rules)
            if (settings.GetValueOrDefault(rule.WhenKey) == rule.WhenValue &&
                !rule.AllowedValues.Contains(settings.GetValueOrDefault(rule.RequireKey)))
                throw new ValidationAppException($"'{rule.RequireKey}' is incompatible with '{rule.WhenKey}'.");
        if (request.Inputs.Count > model.Inputs.Sum(s => s.MaxCount)) throw new ValidationAppException("Too many input images.");
        foreach (var input in request.Inputs)
        {
            if (input == null || model.Inputs.All(i => i.Role != input.Role)) throw new ValidationAppException("Unsupported image input capability.");
            ValidatePngInput(input.Data);
        }
        foreach (var spec in model.Inputs)
            if (request.Inputs.Count(i => i.Role == spec.Role) > spec.MaxCount) throw new ValidationAppException($"Too many '{spec.Label}' images.");
        if (request.Inputs.Any(i => i.Role == "lastFrame") && !request.Inputs.Any(i => i.Role == "firstFrame"))
            throw new ValidationAppException("Last frame requires a first frame.");
        if (model.MediaType == "video" && request.Inputs.Any(i => i.Role == "lastFrame") && settings.GetValueOrDefault("duration") != "8")
            throw new ValidationAppException("First/last frame interpolation requires 8 seconds.");
        return new(prompt, settings, request.Inputs.ToList());
    }

    public static byte[] ValidatePngInput(string data)
    {
        const string prefix = "data:image/png;base64,";
        if (data == null || !data.StartsWith(prefix) || data.Length > 2_800_000)
            throw new ValidationAppException("Reference images must be PNG data with a maximum of 2 MiB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(data[prefix.Length..]); }
        catch (FormatException) { throw new ValidationAppException("Invalid reference image encoding."); }
        if (bytes.Length < 33 || bytes.Length > 2 * 1024 * 1024 ||
            !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)) throw new ValidationAppException("Invalid PNG reference image.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width is < 1 or > 1024 || height is < 1 or > 1024) throw new ValidationAppException("Reference images must fit within 1024 × 1024 pixels.");
        return bytes;
    }

    public static (int Width, int Height) Dimensions(Dictionary<string, string> settings)
    {
        var scale = settings.GetValueOrDefault("resolution") == "4MP" ? 2 : 1;
        var dims = settings.GetValueOrDefault("aspectRatio") switch
        {
            "16:9" => (1344, 768), "9:16" => (768, 1344), _ => (1024, 1024)
        };
        return (dims.Item1 * scale, dims.Item2 * scale);
    }

    public static GenerationQuote Quote(GenerationModel model, ValidatedGeneration input, DateTime now)
    {
        if (GenerationLifecycle.IsRetired(model, now)) throw new ValidationAppException("Model endpoint is retired. Migration required.");
        var pricing = model.Pricing;
        var resolution = input.Settings.GetValueOrDefault("resolution", "1MP");
        if (!pricing.RatesUsd.TryGetValue(resolution, out var cost)) throw new ValidationAppException("Pricing unavailable for this resolution.");
        if (pricing.Unit == "megapixel")
        {
            var (w, h) = Dimensions(input.Settings);
            cost += (decimal)Math.Ceiling(w * (double)h / 1048576) * pricing.ExtraMegapixelUsd - pricing.ExtraMegapixelUsd;
        }
        if (pricing.Unit == "second") cost *= int.Parse(input.Settings["duration"]);
        cost += input.Inputs.Count * pricing.ReferenceUsd;
        if (cost < 0 || pricing.CreditValueUsd <= 0 || pricing.TargetMargin is < 0 or >= 1 || pricing.RiskMultiplier < 1 || pricing.OverheadUsd < 0)
            throw new ValidationAppException("Invalid pricing policy.");
        var internalCost = cost * pricing.RiskMultiplier + pricing.OverheadUsd;
        var credits = model.Provider == "fixture" ? 1 : Math.Max(1, checked((int)decimal.Ceiling(internalCost / (1 - pricing.TargetMargin) / pricing.CreditValueUsd)));
        var fingerprint = Fingerprint(model, input);
        return new(fingerprint + ":" + now.AddMinutes(10).Ticks, model.Id, model.Version, credits, cost,
            internalCost, pricing.Revision, now.AddMinutes(10), input.Settings);
    }
    public static string Fingerprint(GenerationModel model, ValidatedGeneration input)
    {
        var canonical = JsonSerializer.Serialize(new { model.Id, model.Version, model.Pricing, input.Prompt,
            Settings = input.Settings.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value), input.Inputs });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
    public static void ValidateQuote(string? quoteId, string fingerprint, DateTime now)
    {
        var parts = quoteId?.Split(':');
        if (parts?.Length != 2 || parts[0] != fingerprint || !long.TryParse(parts[1], out var ticks) ||
            ticks <= now.Ticks || ticks > now.AddMinutes(11).Ticks)
            throw new ConflictAppException("Quote expired or settings changed. Request a new quote.");
    }
    public static bool IsTerminal(GenerationStatus status) => status is GenerationStatus.Completed or GenerationStatus.Failed or GenerationStatus.Cancelled;
    public static string NormalizeLegacyStatus(string status) => status switch
    {
        "Created" => "Queued", "Running" or "Pending" => "Processing", _ => status
    };
}
