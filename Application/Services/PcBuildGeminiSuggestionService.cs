using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common;
using Application.DTOs.Components;
using Application.DTOs.Recommendations;
using Application.Interfaces.Services;
using Application.Recommendations;
using Microsoft.Extensions.Configuration;

namespace Application.Services;

public sealed class PcBuildGeminiSuggestionService : IPcBuildGeminiSuggestionService
{
    private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const string DefaultModel = "models/gemini-2.5-flash-lite";

    private static readonly HashSet<string> AllowedModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "gemini-2.5-flash",
        "gemini-2.5-pro",
        "gemini-2.0-flash",
        "gemini-2.0-flash-001",
        "gemini-2.0-flash-lite-001",
        "gemini-2.0-flash-lite",
        "gemini-2.5-flash-lite",
    };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPcComponentsService _pcComponentsService;

    public PcBuildGeminiSuggestionService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IPcComponentsService pcComponentsService)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _pcComponentsService = pcComponentsService;
    }

    public async Task<Result<PcBuildGeminiSuggestResponse>> SuggestAsync(PcBuildGeminiSuggestRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            return Result<PcBuildGeminiSuggestResponse>.Fail("VALIDATION_ERROR", "Request is required.");

        var effectiveMessage = BuildMessageFromSelections(request);
        if (string.IsNullOrWhiteSpace(effectiveMessage))
            return Result<PcBuildGeminiSuggestResponse>.Fail("VALIDATION_ERROR", "Please select budget and usage (or provide Message).");

        var apiKey = _configuration["BotChat:KeyBot"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result<PcBuildGeminiSuggestResponse>.Fail("CONFIG_ERROR", "Gemini API key is missing (BotChat:KeyBot).");

        var catalogResult = await _pcComponentsService.GetPcComponentsCatalogAsync(cancellationToken);
        if (!catalogResult.IsSuccess || catalogResult.Value is null)
            return Result<PcBuildGeminiSuggestResponse>.Fail("DATA_ERROR", "Failed to load components catalog.");

        var perTypeCandidates = Math.Clamp(request.PerTypeCandidates, 5, 40);
        var perTypeReturn = Math.Clamp(request.PerTypeReturn, 1, 20);
        var shortlistSize = Math.Clamp(request.ShortlistSize, 5, 10);
        var maxGeneratedBuilds = Math.Clamp(request.MaxGeneratedBuilds, 100, 1500);
        var maxCheckedCombinations = Math.Clamp(request.MaxCheckedCombinations, 10_000, 300_000);

        var budget = ParseBudgetRange(request.Budget);

        var filtered = ApplyRuleBasedFiltering(catalogResult.Value, request, perTypeCandidates, budget);
        var generatedBuilds = GenerateCompatibleBuilds(filtered, maxGeneratedBuilds, maxCheckedCombinations);

        // Retry with broader candidate set before falling back.
        if (generatedBuilds.Count == 0)
        {
            var relaxedCandidates = Math.Clamp(perTypeCandidates * 2, 10, 80);
            var relaxedFiltered = ApplyRuleBasedFiltering(catalogResult.Value, request, relaxedCandidates, budget);
            generatedBuilds = GenerateCompatibleBuilds(
                relaxedFiltered,
                Math.Clamp(maxGeneratedBuilds * 2, 200, 3000),
                Math.Clamp(maxCheckedCombinations * 3, 30_000, 600_000));
        }

        string? fallbackReason = null;
        List<BuildConfig> shortlisted;
        if (generatedBuilds.Count == 0)
        {
            var fallbackBuild = BuildBestEffortBuild(catalogResult.Value, budget);
            if (fallbackBuild is null)
                return Result<PcBuildGeminiSuggestResponse>.Fail("NO_BUILD", "No build can be created from current component data.");

            fallbackBuild.Score = ScoreBuild(fallbackBuild, request, budget);
            shortlisted = new List<BuildConfig> { fallbackBuild };
            fallbackReason = "No fully compatible build found, returned best-effort build from available components.";
        }
        else
        {
            foreach (var build in generatedBuilds)
                build.Score = ScoreBuild(build, request, budget);

            shortlisted = generatedBuilds
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.TotalPrice)
                .Take(shortlistSize)
                .ToList();
        }

        var requestedModel = NormalizeModelName(request.Model);
        var modelToUse = IsAllowedModel(requestedModel) ? requestedModel! : NormalizeModelName(DefaultModel) ?? "gemini-2.5-flash-lite";
        if (!IsAllowedModel(modelToUse))
            modelToUse = AllowedModels.First();

        var geminiPick = shortlisted.Count > 1
            ? await ChooseFinalBuildByGeminiAsync(apiKey, modelToUse, effectiveMessage, shortlisted, cancellationToken)
            : (PickedBuild: shortlisted.First(), Reason: fallbackReason, Raw: fallbackReason);
        var finalBuild = geminiPick.PickedBuild ?? shortlisted.First();

        var response = new PcBuildGeminiSuggestResponse
        {
            ModelUsed = modelToUse,
            Raw = geminiPick.Raw,
            FinalReason = string.IsNullOrWhiteSpace(geminiPick.Reason) ? fallbackReason : geminiPick.Reason,
            FinalBuild = ToCandidateDto(finalBuild),
            ShortlistedBuilds = shortlisted.Select(ToCandidateDto).ToList(),
        };

        response.Cpus = shortlisted.Select(x => x.Cpu).Where(x => x is not null).Cast<CpuDto>().DistinctBy(x => x.Id).Take(perTypeReturn).ToList();
        response.Gpus = shortlisted.Select(x => x.Gpu).Where(x => x is not null).Cast<GpuDto>().DistinctBy(x => x.Id).Take(perTypeReturn).ToList();
        response.Rams = shortlisted.Select(x => x.Ram).Where(x => x is not null).Cast<RamDto>().DistinctBy(x => x.Id).Take(perTypeReturn).ToList();
        response.Storages = shortlisted.Select(x => x.Storage).Where(x => x is not null).Cast<StorageDto>().DistinctBy(x => x.Id).Take(perTypeReturn).ToList();
        response.Motherboards = shortlisted.Select(x => x.Motherboard).Where(x => x is not null).Cast<MotherboardDto>().DistinctBy(x => x.Id).Take(perTypeReturn).ToList();
        response.Psus = shortlisted.Select(x => x.Psu).Where(x => x is not null).Cast<PsuDto>().DistinctBy(x => x.Id).Take(perTypeReturn).ToList();
        response.Cases = shortlisted.Select(x => x.Case).Where(x => x is not null).Cast<CaseDto>().DistinctBy(x => x.Id).Take(perTypeReturn).ToList();
        response.Coolings = shortlisted.Select(x => x.Cooling).Where(x => x is not null).Cast<CoolingDto>().DistinctBy(x => x.Id).Take(perTypeReturn).ToList();

        return Result<PcBuildGeminiSuggestResponse>.Ok(response);
    }

    private static PcComponentsCatalogDto ApplyRuleBasedFiltering(PcComponentsCatalogDto catalog, PcBuildGeminiSuggestRequest request, int perTypeCandidates, BudgetRange budget)
    {
        var usage = (request.Usage ?? string.Empty).ToLowerInvariant();
        var perfTags = request.PerformanceTags?.Select(x => x.ToLowerInvariant()).ToList() ?? new List<string>();
        var brandPref = (request.BrandPreference ?? string.Empty).ToLowerInvariant();

        bool officeLike = usage.Contains("office") || usage.Contains("study");
        bool gamingLike = usage.Contains("gaming") || perfTags.Any(x => x.Contains("fps"));
        bool creatorLike = usage.Contains("design") || usage.Contains("video") || perfTags.Any(x => x.Contains("multi"));
        bool aiLike = usage.Contains("ai") || usage.Contains("program") || usage.Contains("dev");

        decimal budgetTarget = budget.Target;

        return new PcComponentsCatalogDto
        {
            Cpus = catalog.Cpus
                .OrderByDescending(x => CpuRuleScore(x, brandPref, gamingLike, creatorLike, aiLike))
                .ThenBy(x => Math.Abs(x.Price - budgetTarget * 0.25m))
                .ThenBy(x => x.Price)
                .Take(perTypeCandidates)
                .ToList(),

            Gpus = catalog.Gpus
                .OrderByDescending(x => GpuRuleScore(x, brandPref, officeLike, gamingLike, creatorLike, aiLike))
                .ThenBy(x => Math.Abs(x.Price - budgetTarget * (gamingLike ? 0.35m : 0.25m)))
                .ThenBy(x => x.Price)
                .Take(perTypeCandidates)
                .ToList(),

            Rams = catalog.Rams
                .OrderByDescending(x => RamRuleScore(x, gamingLike, creatorLike, aiLike))
                .ThenBy(x => Math.Abs(x.Price - budgetTarget * 0.1m))
                .ThenBy(x => x.Price)
                .Take(perTypeCandidates)
                .ToList(),

            Storages = catalog.Storages
                .OrderByDescending(x => StorageRuleScore(x, gamingLike, creatorLike, aiLike))
                .ThenBy(x => Math.Abs(x.Price - budgetTarget * 0.1m))
                .ThenBy(x => x.Price)
                .Take(perTypeCandidates)
                .ToList(),

            Motherboards = catalog.Motherboards.OrderBy(x => x.Price).Take(perTypeCandidates).ToList(),
            Psus = catalog.Psus.OrderByDescending(x => x.Wattage).ThenBy(x => x.Price).Take(perTypeCandidates).ToList(),
            Cases = catalog.Cases.OrderBy(x => x.Price).Take(perTypeCandidates).ToList(),
            Coolings = catalog.Coolings.OrderByDescending(x => x.TdpSupport).ThenBy(x => x.Price).Take(perTypeCandidates).ToList(),
        };
    }

    private static List<BuildConfig> GenerateCompatibleBuilds(PcComponentsCatalogDto filtered, int maxGeneratedBuilds, int maxCheckedCombinations)
    {
        var checker = new CompatibilityChecker();
        var output = new List<BuildConfig>();
        var checkedCount = 0;

        var gpus = filtered.Gpus.Count > 0 ? filtered.Gpus.Cast<GpuDto?>().ToList() : new List<GpuDto?> { null };
        var cases = filtered.Cases.Count > 0 ? filtered.Cases.Cast<CaseDto?>().ToList() : new List<CaseDto?> { null };
        var coolings = filtered.Coolings.Count > 0 ? filtered.Coolings.Cast<CoolingDto?>().ToList() : new List<CoolingDto?> { null };

        foreach (var cpu in filtered.Cpus)
        foreach (var motherboard in filtered.Motherboards)
        foreach (var ram in filtered.Rams)
        foreach (var storage in filtered.Storages)
        foreach (var psu in filtered.Psus)
        foreach (var gpu in gpus)
        foreach (var pcCase in cases)
        foreach (var cooling in coolings)
        {
            checkedCount++;
            if (checkedCount > maxCheckedCombinations)
                return output;

            var config = new BuildConfig
            {
                Cpu = cpu,
                Motherboard = motherboard,
                Ram = ram,
                Storage = storage,
                Psu = psu,
                Gpu = gpu,
                Case = pcCase,
                Cooling = cooling,
            };

            if (checker.IsCompatible(config))
                output.Add(config);

            if (output.Count >= maxGeneratedBuilds)
                return output;
        }

        return output;
    }

    private static double ScoreBuild(BuildConfig build, PcBuildGeminiSuggestRequest request, BudgetRange budget)
    {
        var usage = (request.Usage ?? string.Empty).ToLowerInvariant();
        var perfTags = request.PerformanceTags?.Select(x => x.ToLowerInvariant()).ToList() ?? new List<string>();
        var budgetTarget = (double)budget.Target;

        var score = 0.0;
        var total = (double)build.TotalPrice;
        var diffRatio = budgetTarget <= 0 ? 0 : Math.Abs(total - budgetTarget) / budgetTarget;
        score += Math.Max(0, 40 - diffRatio * 100);

        if (build.TotalPrice >= budget.Min && (budget.Max <= 0 || build.TotalPrice <= budget.Max))
            score += 20;

        if (!string.IsNullOrWhiteSpace(build.Cpu?.Socket)) score += 3;
        if (!string.IsNullOrWhiteSpace(build.Motherboard?.Socket)) score += 3;
        if (!string.IsNullOrWhiteSpace(build.Ram?.Type)) score += 2;

        var gpuPower = build.Gpu?.PowerConsumption ?? 0;
        var gpuVram = build.Gpu?.Vram ?? 0;
        var cpuCores = build.Cpu?.Cores ?? 0;
        var ramCap = build.Ram?.Capacity ?? 0;
        var storageCap = build.Storage?.Capacity ?? 0;

        if (usage.Contains("gaming") || perfTags.Any(x => x.Contains("fps")))
            score += gpuVram * 1.8 + gpuPower * 0.06 + cpuCores * 0.7;

        if (usage.Contains("design") || usage.Contains("video") || perfTags.Any(x => x.Contains("multi")))
            score += cpuCores * 1.3 + ramCap * 0.8 + storageCap * 0.02;

        if (usage.Contains("ai") || usage.Contains("program") || usage.Contains("dev"))
            score += cpuCores * 1.5 + ramCap * 0.9 + gpuVram * 1.0;

        if (usage.Contains("office") || usage.Contains("study"))
            score += total < budgetTarget ? 10 : 2;

        var estimatedPower = (build.Cpu?.Tdp ?? 65) + (build.Gpu?.PowerConsumption ?? 0) + 90;
        var psuHeadroom = (build.Psu?.Wattage ?? 0) - estimatedPower;
        score += psuHeadroom >= 150 ? 8 : psuHeadroom >= 80 ? 4 : 0;

        return score;
    }

    private async Task<(BuildConfig? PickedBuild, string? Reason, string? Raw)> ChooseFinalBuildByGeminiAsync(
        string apiKey,
        string model,
        string message,
        List<BuildConfig> shortlisted,
        CancellationToken cancellationToken)
    {
        if (shortlisted.Count == 0)
            return (null, null, null);

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = "You are a PC build advisor. Return strict JSON only." } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = BuildFinalPickPrompt(message, shortlisted) } }
                }
            }
        };

        var client = _httpClientFactory.CreateClient("Gemini");
        var url = $"{GeminiUrl}{model}:generateContent?key={apiKey}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };

        using var httpResponse = await client.SendAsync(httpRequest, cancellationToken);
        var raw = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
            return (shortlisted.First(), "Gemini unavailable, fallback to top score.", raw);

        string? text = null;
        try
        {
            var parsed = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            text = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?
                .Select(p => p.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Aggregate("", (acc, cur) => string.IsNullOrEmpty(acc) ? cur!.Trim() : $"{acc}\n{cur!.Trim()}");
        }
        catch (JsonException)
        {
        }

        if (string.IsNullOrWhiteSpace(text))
            return (shortlisted.First(), "Gemini empty output, fallback to top score.", raw);

        var json = ExtractFirstJsonObject(text);
        if (json is null)
            return (shortlisted.First(), "Gemini malformed output, fallback to top score.", text);

        try
        {
            var pick = JsonSerializer.Deserialize<GeminiFinalPick>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (pick is null || pick.BuildIndex < 0 || pick.BuildIndex >= shortlisted.Count)
                return (shortlisted.First(), "Gemini invalid index, fallback to top score.", json);

            return (shortlisted[pick.BuildIndex], pick.Reason, json);
        }
        catch (JsonException)
        {
            return (shortlisted.First(), "Gemini parse error, fallback to top score.", json);
        }
    }

    private static string BuildFinalPickPrompt(string message, List<BuildConfig> builds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Select exactly one final build from the scored shortlist.");
        sb.AppendLine("Return JSON only:");
        sb.AppendLine("{");
        sb.AppendLine("  \"buildIndex\": 0,");
        sb.AppendLine("  \"reason\": \"...\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("User intent:");
        sb.AppendLine(message.Trim());
        sb.AppendLine();
        sb.AppendLine("Build shortlist (index starts at 0):");

        for (var i = 0; i < builds.Count; i++)
        {
            var b = builds[i];
            sb.AppendLine($"- index={i}, score={b.Score:F2}, totalPrice={b.TotalPrice}, " +
                          $"cpu={{id:{b.Cpu?.Id},brand:{b.Cpu?.Brand},cores:{b.Cpu?.Cores}}}, " +
                          $"gpu={{id:{b.Gpu?.Id},brand:{b.Gpu?.Brand},vram:{b.Gpu?.Vram}}}, " +
                          $"ram={{id:{b.Ram?.Id},cap:{b.Ram?.Capacity},type:{b.Ram?.Type}}}, " +
                          $"storage={{id:{b.Storage?.Id},cap:{b.Storage?.Capacity},iface:{b.Storage?.Interface}}}, " +
                          $"mb={{id:{b.Motherboard?.Id},socket:{b.Motherboard?.Socket},ramType:{b.Motherboard?.RamType}}}, " +
                          $"psu={{id:{b.Psu?.Id},watt:{b.Psu?.Wattage}}}");
        }

        return sb.ToString().Trim();
    }

    private static PcBuildCandidateDto ToCandidateDto(BuildConfig b)
        => new()
        {
            TotalPrice = b.TotalPrice,
            Score = b.Score,
            Cpu = b.Cpu,
            Gpu = b.Gpu,
            Ram = b.Ram,
            Storage = b.Storage,
            Motherboard = b.Motherboard,
            Psu = b.Psu,
            Case = b.Case,
            Cooling = b.Cooling,
        };

    private static int CpuRuleScore(CpuDto x, string brandPref, bool gamingLike, bool creatorLike, bool aiLike)
    {
        var score = 0;
        if (brandPref.Contains("intel") && x.Brand.Contains("intel", StringComparison.OrdinalIgnoreCase)) score += 6;
        if (brandPref.Contains("amd") && x.Brand.Contains("amd", StringComparison.OrdinalIgnoreCase)) score += 6;
        if (gamingLike) score += x.BoostClock >= 5.0 ? 8 : x.BoostClock >= 4.5 ? 4 : 0;
        if (creatorLike || aiLike) score += x.Cores >= 12 ? 8 : x.Cores >= 8 ? 5 : 1;
        return score;
    }

    private static int GpuRuleScore(GpuDto x, string brandPref, bool officeLike, bool gamingLike, bool creatorLike, bool aiLike)
    {
        var score = 0;
        if (brandPref.Contains("nvidia") && x.Brand.Contains("nvidia", StringComparison.OrdinalIgnoreCase)) score += 6;
        if (brandPref.Contains("amd") && x.Brand.Contains("amd", StringComparison.OrdinalIgnoreCase)) score += 6;
        if (officeLike) score -= x.PowerConsumption > 220 ? 4 : 0;
        if (gamingLike) score += x.Vram >= 12 ? 10 : x.Vram >= 8 ? 6 : 2;
        if (creatorLike || aiLike) score += x.Vram >= 16 ? 8 : x.Vram >= 12 ? 4 : 0;
        return score;
    }

    private static int RamRuleScore(RamDto x, bool gamingLike, bool creatorLike, bool aiLike)
    {
        var score = 0;
        if (gamingLike) score += x.Speed >= 6000 ? 6 : x.Speed >= 5200 ? 3 : 1;
        if (creatorLike || aiLike) score += x.Capacity >= 32 ? 8 : x.Capacity >= 16 ? 4 : 1;
        return score;
    }

    private static int StorageRuleScore(StorageDto x, bool gamingLike, bool creatorLike, bool aiLike)
    {
        var score = 0;
        if (x.Interface.Contains("pcie", StringComparison.OrdinalIgnoreCase) || x.Interface.Contains("nvme", StringComparison.OrdinalIgnoreCase)) score += 4;
        if (gamingLike || creatorLike || aiLike) score += x.Capacity >= 1000 ? 5 : x.Capacity >= 512 ? 2 : 0;
        return score;
    }

    private static BuildConfig? BuildBestEffortBuild(PcComponentsCatalogDto catalog, BudgetRange budget)
    {
        if (catalog.Cpus.Count == 0 || catalog.Motherboards.Count == 0 || catalog.Rams.Count == 0 ||
            catalog.Storages.Count == 0 || catalog.Psus.Count == 0)
            return null;

        var target = budget.Target <= 0 ? 20_000_000m : budget.Target;

        return new BuildConfig
        {
            Cpu = PickClosestByPrice(catalog.Cpus, target * 0.25m),
            Motherboard = PickClosestByPrice(catalog.Motherboards, target * 0.15m),
            Ram = PickClosestByPrice(catalog.Rams, target * 0.12m),
            Storage = PickClosestByPrice(catalog.Storages, target * 0.12m),
            Psu = PickClosestByPrice(catalog.Psus, target * 0.10m),
            Gpu = catalog.Gpus.Count > 0 ? PickClosestByPrice(catalog.Gpus, target * 0.28m) : null,
            Case = catalog.Cases.Count > 0 ? PickClosestByPrice(catalog.Cases, target * 0.08m) : null,
            Cooling = catalog.Coolings.Count > 0 ? PickClosestByPrice(catalog.Coolings, target * 0.05m) : null,
        };
    }

    private static T PickClosestByPrice<T>(IEnumerable<T> items, decimal targetPrice) where T : BaseComponentDto
        => items.OrderBy(x => Math.Abs(x.Price - targetPrice)).ThenBy(x => x.Price).First();

    private static string BuildMessageFromSelections(PcBuildGeminiSuggestRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Message))
            return request.Message!.Trim();

        var budget = (request.Budget ?? "").Trim();
        var usage = (request.Usage ?? "").Trim();
        var brandPref = (request.BrandPreference ?? "").Trim();
        var perfTags = request.PerformanceTags?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (string.IsNullOrWhiteSpace(budget) || string.IsNullOrWhiteSpace(usage))
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("PC build request from UI:");
        sb.AppendLine($"- Budget: {budget}");
        sb.AppendLine($"- Usage: {usage}");
        if (perfTags.Count > 0) sb.AppendLine($"- Performance tags: {string.Join(", ", perfTags)}");
        if (!string.IsNullOrWhiteSpace(brandPref)) sb.AppendLine($"- Brand preference: {brandPref}");
        return sb.ToString().Trim();
    }

    private static BudgetRange ParseBudgetRange(string? budget)
    {
        var b = (budget ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(b)) return new BudgetRange(0, 0, 20_000_000m);

        if (b.Contains("duoi") || b.Contains("under")) return new BudgetRange(0, 15_000_000m, 12_000_000m);
        if (b.Contains("15") && b.Contains("25")) return new BudgetRange(15_000_000m, 25_000_000m, 20_000_000m);
        if (b.Contains("25") && b.Contains("40")) return new BudgetRange(25_000_000m, 40_000_000m, 32_000_000m);
        if (b.Contains("tren") || b.Contains("over") || b.Contains("40")) return new BudgetRange(40_000_000m, 0, 45_000_000m);

        return new BudgetRange(0, 0, 20_000_000m);
    }

    private static string? ExtractFirstJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return text[start..(i + 1)].Trim();
            }
        }
        return null;
    }

    private static string? NormalizeModelName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return null;
        model = model.Trim();
        return model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model["models/".Length..]
            : model;
    }

    private static bool IsAllowedModel(string? normalizedModel)
        => !string.IsNullOrWhiteSpace(normalizedModel) && AllowedModels.Contains(normalizedModel);

    private sealed class GeminiGenerateContentResponse
    {
        public List<GeminiCandidate> Candidates { get; set; } = new();
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        public List<GeminiPart> Parts { get; set; } = new();
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class GeminiFinalPick
    {
        public int BuildIndex { get; set; }
        public string? Reason { get; set; }
    }

    private readonly record struct BudgetRange(decimal Min, decimal Max, decimal Target);
}
