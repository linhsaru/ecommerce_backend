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
        var beamWidth = Math.Clamp(request.MaxCheckedCombinations / 200, 30, 240);

        var budget = ParseBudgetRange(request.Budget);
        var cleanedCatalog = CleanCatalog(catalogResult.Value);

        var ruleProfiles = new[]
        {
            BuildRuleProfile.Strict,
            BuildRuleProfile.RelaxedUnknownFields,
            BuildRuleProfile.RelaxedPsuUpperBound,
            BuildRuleProfile.MostRelaxed,
        };

        var generatedBuilds = new List<BuildConfig>();
        string? fallbackReason = null;
        foreach (var rule in ruleProfiles)
        {
            var filtered = ApplyDependencyAwareFiltering(cleanedCatalog, request, perTypeCandidates, rule);
            generatedBuilds = GenerateCompatibleBuildsBeamSearch(filtered, request, budget, beamWidth, maxGeneratedBuilds, rule);
            if (generatedBuilds.Count > 0)
                break;

            fallbackReason = rule.RelaxNote;
        }

        List<BuildConfig> shortlisted;
        if (generatedBuilds.Count == 0)
        {
            var fallbackBuild = BuildBestEffortCompatibleBuild(cleanedCatalog, request, budget);
            if (fallbackBuild is null)
                return Result<PcBuildGeminiSuggestResponse>.Fail("NO_BUILD", "No build can be created from current component data.");

            fallbackBuild.Score = ScoreBuild(fallbackBuild, request, budget);
            shortlisted = new List<BuildConfig> { fallbackBuild };
            fallbackReason ??= "No build found with strict rules; returned best compatible build under relaxed rules.";
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

    private static PcComponentsCatalogDto ApplyDependencyAwareFiltering(PcComponentsCatalogDto catalog, PcBuildGeminiSuggestRequest request, int perTypeCandidates, BuildRuleProfile profile)
    {
        var usage = (request.Usage ?? string.Empty).ToLowerInvariant();
        var perfTags = request.PerformanceTags?.Select(x => x.ToLowerInvariant()).ToList() ?? new List<string>();
        var brandPref = (request.BrandPreference ?? string.Empty).ToLowerInvariant();

        bool officeLike = usage.Contains("office") || usage.Contains("study");
        bool gamingLike = usage.Contains("gaming") || perfTags.Any(x => x.Contains("fps"));
        bool creatorLike = usage.Contains("design") || usage.Contains("video") || perfTags.Any(x => x.Contains("multi"));
        bool aiLike = usage.Contains("ai") || usage.Contains("program") || usage.Contains("dev");

        var rankedCpus = catalog.Cpus
            .OrderByDescending(x => CpuRuleScore(x, brandPref, gamingLike, creatorLike, aiLike))
            .ThenByDescending(x => x.Cores)
            .ThenByDescending(x => x.BoostClock)
            .ThenBy(x => x.Price)
            .Take(perTypeCandidates)
            .ToList();

        var cpuSocketSet = rankedCpus
            .Select(x => NormalizeSocket(x.Socket))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var motherboards = catalog.Motherboards
            .Where(mb =>
            {
                var mbSocket = NormalizeSocket(mb.Socket);
                if (string.IsNullOrWhiteSpace(mbSocket))
                    return profile.AllowUnknownSocket;
                return cpuSocketSet.Contains(mbSocket);
            })
            .OrderBy(x => x.Price)
            .ThenByDescending(x => x.RamSlots)
            .Take(perTypeCandidates * 2)
            .ToList();

        if (motherboards.Count == 0)
            motherboards = catalog.Motherboards.OrderBy(x => x.Price).Take(perTypeCandidates * 2).ToList();

        var mbRamTypeSet = motherboards
            .Select(x => NormalizeRamType(x.RamType))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rams = catalog.Rams
            .Where(r =>
            {
                var ramType = NormalizeRamType(r.Type);
                if (string.IsNullOrWhiteSpace(ramType))
                    return profile.AllowUnknownRamType;
                return mbRamTypeSet.Contains(ramType);
            })
            .OrderByDescending(x => RamRuleScore(x, gamingLike, creatorLike, aiLike))
            .ThenByDescending(x => x.Capacity)
            .ThenByDescending(x => x.Speed)
            .ThenBy(x => x.Price)
            .Take(perTypeCandidates * 2)
            .ToList();

        if (rams.Count == 0)
            rams = catalog.Rams.OrderBy(x => x.Price).Take(perTypeCandidates * 2).ToList();

        return new PcComponentsCatalogDto
        {
            Cpus = rankedCpus,
            Motherboards = motherboards,
            Rams = rams,
            Gpus = catalog.Gpus
                .OrderByDescending(x => GpuRuleScore(x, brandPref, officeLike, gamingLike, creatorLike, aiLike))
                .ThenBy(x => x.Price)
                .Take(perTypeCandidates * 2)
                .ToList(),
            Storages = catalog.Storages
                .OrderByDescending(x => StorageRuleScore(x, gamingLike, creatorLike, aiLike))
                .ThenBy(x => x.Price)
                .Take(perTypeCandidates * 2)
                .ToList(),
            Psus = catalog.Psus.OrderBy(x => x.Wattage).ThenBy(x => x.Price).Take(perTypeCandidates * 3).ToList(),
            Cases = catalog.Cases.OrderBy(x => x.Price).Take(perTypeCandidates * 2).ToList(),
            Coolings = catalog.Coolings.OrderByDescending(x => x.TdpSupport).ThenBy(x => x.Price).Take(perTypeCandidates * 2).ToList(),
        };
    }

    private static List<BuildConfig> GenerateCompatibleBuildsBeamSearch(
        PcComponentsCatalogDto filtered,
        PcBuildGeminiSuggestRequest request,
        BudgetRange budget,
        int beamWidth,
        int maxGeneratedBuilds,
        BuildRuleProfile profile)
    {
        var checker = new CompatibilityChecker();
        var costModel = CreateBuildStageCostModel(filtered);
        var beam = filtered.Cpus
            .Select(cpu => new BuildConfig { Cpu = cpu })
            .Where(checker.IsPartiallyCompatible)
            .ToList();

        beam = ExpandBeam(
            beam,
            cpuBuild => FilterMotherboardsByCpu(filtered.Motherboards, cpuBuild.Cpu!, profile).Select(mb => new BuildConfig { Cpu = cpuBuild.Cpu, Motherboard = mb }),
            beamWidth,
            request,
            budget,
            BuildStage.CpuMotherboard,
            costModel,
            checker);

        beam = ExpandBeam(
            beam,
            partial => FilterRamsByMotherboard(filtered.Rams, partial.Motherboard!, profile).Select(ram => new BuildConfig { Cpu = partial.Cpu, Motherboard = partial.Motherboard, Ram = ram }),
            beamWidth,
            request,
            budget,
            BuildStage.Ram,
            costModel,
            checker);

        beam = ExpandBeam(
            beam,
            partial => filtered.Storages.Select(storage => new BuildConfig
            {
                Cpu = partial.Cpu,
                Motherboard = partial.Motherboard,
                Ram = partial.Ram,
                Storage = storage
            }),
            beamWidth,
            request,
            budget,
            BuildStage.Storage,
            costModel,
            checker);

        var gpuOptions = filtered.Gpus.Count > 0 ? filtered.Gpus.Cast<GpuDto?>().ToList() : new List<GpuDto?> { null };
        beam = ExpandBeam(
            beam,
            partial => gpuOptions.Select(gpu => new BuildConfig
            {
                Cpu = partial.Cpu,
                Motherboard = partial.Motherboard,
                Ram = partial.Ram,
                Storage = partial.Storage,
                Gpu = gpu
            }),
            beamWidth,
            request,
            budget,
            BuildStage.Gpu,
            costModel,
            checker);

        beam = ExpandBeam(
            beam,
            partial => FilterPsusByPowerAndConnectors(filtered.Psus, partial.Cpu!, partial.Gpu, profile).Select(psu => new BuildConfig
            {
                Cpu = partial.Cpu,
                Motherboard = partial.Motherboard,
                Ram = partial.Ram,
                Storage = partial.Storage,
                Gpu = partial.Gpu,
                Psu = psu
            }),
            beamWidth,
            request,
            budget,
            BuildStage.Psu,
            costModel,
            checker);

        var caseOptions = filtered.Cases.Count > 0 ? filtered.Cases.Cast<CaseDto?>().ToList() : new List<CaseDto?> { null };
        beam = ExpandBeam(
            beam,
            partial => FilterCasesByMotherboard(caseOptions, partial.Motherboard!).Select(pcCase => new BuildConfig
            {
                Cpu = partial.Cpu,
                Motherboard = partial.Motherboard,
                Ram = partial.Ram,
                Storage = partial.Storage,
                Gpu = partial.Gpu,
                Psu = partial.Psu,
                Case = pcCase
            }),
            beamWidth,
            request,
            budget,
            BuildStage.Case,
            costModel,
            checker);

        var coolingOptions = filtered.Coolings.Count > 0 ? filtered.Coolings.Cast<CoolingDto?>().ToList() : new List<CoolingDto?> { null };
        beam = ExpandBeam(
            beam,
            partial => FilterCoolingsByCpu(coolingOptions, partial.Cpu!).Select(cooling => new BuildConfig
            {
                Cpu = partial.Cpu,
                Motherboard = partial.Motherboard,
                Ram = partial.Ram,
                Storage = partial.Storage,
                Gpu = partial.Gpu,
                Psu = partial.Psu,
                Case = partial.Case,
                Cooling = cooling
            }),
            Math.Clamp(beamWidth * 2, 60, 400),
            request,
            budget,
            BuildStage.Cooling,
            costModel,
            checker);

        return beam
            .Where(x => x.Cpu is not null && x.Motherboard is not null && x.Ram is not null && x.Storage is not null && x.Psu is not null)
            .Where(x => checker.IsCompatible(x))
            .Where(x => IsFinalBudgetAcceptable(x, budget))
            .OrderByDescending(x => ScoreBuild(x, request, budget))
            .ThenBy(x => x.TotalPrice)
            .Take(maxGeneratedBuilds)
            .ToList();
    }

    private static bool IsSocketCompatible(string? cpuSocket, string? motherboardSocket)
    {
        var a = NormalizeSocket(cpuSocket);
        var b = NormalizeSocket(motherboardSocket);
        return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRamTypeCompatible(string? ramType, string? motherboardRamType)
    {
        var a = NormalizeRamType(ramType);
        var b = NormalizeRamType(motherboardRamType);
        return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSocket(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var v = value.Trim().ToUpperInvariant();
        return v.Replace("SOCKET", string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
    }

    private static string NormalizeRamType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var v = value.Trim().ToUpperInvariant();
        if (v.Contains("DDR5")) return "DDR5";
        if (v.Contains("DDR4")) return "DDR4";
        if (v.Contains("DDR3")) return "DDR3";
        return v.Replace(" ", string.Empty).Replace("-", string.Empty);
    }

    private static PcComponentsCatalogDto CleanCatalog(PcComponentsCatalogDto catalog)
    {
        var cleaned = new PcComponentsCatalogDto
        {
            Cpus = catalog.Cpus
                .Where(x => x.Id != Guid.Empty && x.Price > 0 && !string.IsNullOrWhiteSpace(NormalizeSocket(x.Socket)))
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList(),
            Motherboards = catalog.Motherboards
                .Where(x => x.Id != Guid.Empty && x.Price > 0)
                .Select(x =>
                {
                    x.Socket = NormalizeSocket(x.Socket);
                    x.RamType = NormalizeRamType(x.RamType);
                    x.FormFactor = NormalizeFormFactor(x.FormFactor);
                    return x;
                })
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList(),
            Rams = catalog.Rams
                .Where(x => x.Id != Guid.Empty && x.Price > 0)
                .Select(x =>
                {
                    x.Type = NormalizeRamType(x.Type);
                    return x;
                })
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList(),
            Storages = catalog.Storages
                .Where(x => x.Id != Guid.Empty && x.Price > 0 && x.Capacity > 0)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList(),
            Gpus = catalog.Gpus
                .Where(x => x.Id != Guid.Empty && x.Price > 0)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList(),
            Psus = catalog.Psus
                .Where(x => x.Id != Guid.Empty && x.Price > 0 && x.Wattage > 0)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList(),
            Cases = catalog.Cases
                .Where(x => x.Id != Guid.Empty && x.Price > 0)
                .Select(x =>
                {
                    x.FormFactor = NormalizeFormFactor(x.FormFactor);
                    x.SupportedFormFactors = x.SupportedFormFactors?
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Select(NormalizeFormFactor)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList() ?? new List<string>();
                    return x;
                })
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList(),
            Coolings = catalog.Coolings
                .Where(x => x.Id != Guid.Empty && x.Price > 0)
                .Select(x =>
                {
                    x.SupportedSockets = x.SupportedSockets?
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(NormalizeSocket)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return x;
                })
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList(),
        };

        return cleaned;
    }

    private static string NormalizeFormFactor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var v = value.Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (v.Contains("MICROATX") || v == "MATX") return "MICROATX";
        if (v.Contains("MINIITX") || v == "ITX") return "MINIITX";
        if (v.Contains("ATX")) return "ATX";
        return v;
    }

    private static List<BuildConfig> ExpandBeam(
        IEnumerable<BuildConfig> seeds,
        Func<BuildConfig, IEnumerable<BuildConfig>> expand,
        int beamWidth,
        PcBuildGeminiSuggestRequest request,
        BudgetRange budget,
        BuildStage stage,
        BuildStageCostModel costModel,
        CompatibilityChecker checker)
    {
        return seeds
            .SelectMany(expand)
            .Where(checker.IsPartiallyCompatible)
            .Where(x => IsPartialBudgetAcceptable(x, budget))
            .Where(x => CanCompleteWithinBudget(x, stage, budget, costModel))
            .OrderByDescending(x => ScorePartialBuild(x, request, budget))
            .ThenBy(x => x.TotalPrice)
            .Take(beamWidth)
            .ToList();
    }

    private static IEnumerable<MotherboardDto> FilterMotherboardsByCpu(IEnumerable<MotherboardDto> motherboards, CpuDto cpu, BuildRuleProfile profile)
    {
        var cpuSocket = NormalizeSocket(cpu.Socket);
        return motherboards.Where(mb =>
        {
            var mbSocket = NormalizeSocket(mb.Socket);
            if (string.IsNullOrWhiteSpace(cpuSocket) || string.IsNullOrWhiteSpace(mbSocket))
                return profile.AllowUnknownSocket;
            return string.Equals(cpuSocket, mbSocket, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static IEnumerable<RamDto> FilterRamsByMotherboard(IEnumerable<RamDto> rams, MotherboardDto motherboard, BuildRuleProfile profile)
    {
        var mbRamType = NormalizeRamType(motherboard.RamType);
        return rams.Where(ram =>
        {
            var ramType = NormalizeRamType(ram.Type);
            if (string.IsNullOrWhiteSpace(mbRamType) || string.IsNullOrWhiteSpace(ramType))
                return profile.AllowUnknownRamType;
            return string.Equals(mbRamType, ramType, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static IEnumerable<PsuDto> FilterPsusByPowerAndConnectors(IEnumerable<PsuDto> psus, CpuDto cpu, GpuDto? gpu, BuildRuleProfile profile)
    {
        var estimatedPower = (cpu.Tdp > 0 ? cpu.Tdp : 65) + (gpu?.PowerConsumption ?? 0) + 90;
        var minRequired = estimatedPower * 1.25;
        var maxAllowed = profile.PsuMaxMultiplier <= 0 ? int.MaxValue : estimatedPower * profile.PsuMaxMultiplier;
        var requiredConnectors = (gpu?.RequiredConnectors ?? new List<string>())
            .Select(NormalizeConnector)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return psus.Where(psu =>
        {
            if (psu.Wattage < minRequired || psu.Wattage > maxAllowed)
                return false;

            if (requiredConnectors.Count == 0)
                return true;

            var available = (psu.PcieConnectors ?? new List<string>())
                .Select(NormalizeConnector)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (available.Count == 0)
                return false;

            return requiredConnectors.All(req => available.Any(av => av.Contains(req, StringComparison.OrdinalIgnoreCase) || req.Contains(av, StringComparison.OrdinalIgnoreCase)));
        });
    }

    private readonly record struct BuildRuleProfile(
        bool AllowUnknownSocket,
        bool AllowUnknownRamType,
        double PsuMaxMultiplier,
        string RelaxNote)
    {
        public static readonly BuildRuleProfile Strict = new(
            AllowUnknownSocket: false,
            AllowUnknownRamType: false,
            PsuMaxMultiplier: 2.2,
            RelaxNote: "Strict dependency rules returned no candidate.");

        public static readonly BuildRuleProfile RelaxedUnknownFields = new(
            AllowUnknownSocket: true,
            AllowUnknownRamType: true,
            PsuMaxMultiplier: 2.2,
            RelaxNote: "Relaxed unknown socket/RAM type fields.");

        public static readonly BuildRuleProfile RelaxedPsuUpperBound = new(
            AllowUnknownSocket: true,
            AllowUnknownRamType: true,
            PsuMaxMultiplier: 3.2,
            RelaxNote: "Relaxed PSU upper-bound rule.");

        public static readonly BuildRuleProfile MostRelaxed = new(
            AllowUnknownSocket: true,
            AllowUnknownRamType: true,
            PsuMaxMultiplier: 4.5,
            RelaxNote: "Most-relaxed rule profile used.");
    }

    private static double ScoreBuild(BuildConfig build, PcBuildGeminiSuggestRequest request, BudgetRange budget)
    {
        var score = ScorePartialBuild(build, request, budget);
        var usage = (request.Usage ?? string.Empty).ToLowerInvariant();
        var total = (double)build.TotalPrice;

        var gpuPower = build.Gpu?.PowerConsumption ?? 0;
        var estimatedPower = (build.Cpu?.Tdp ?? 65) + (build.Gpu?.PowerConsumption ?? 0) + 90;
        var psuHeadroom = (build.Psu?.Wattage ?? 0) - estimatedPower;
        score += psuHeadroom >= 150 ? 8 : psuHeadroom >= 80 ? 4 : 0;

        // Final-only budget penalties/rewards to strongly avoid over-budget builds.
        if (budget.Max > 0 && build.TotalPrice > budget.Max)
        {
            var overRatio = (double)(build.TotalPrice - budget.Max) / (double)budget.Max;
            score -= 80 + overRatio * 300;
        }
        else if (build.TotalPrice >= budget.Min)
        {
            score += 12;
        }

        if (usage.Contains("office") || usage.Contains("study"))
            score += total <= (double)budget.Target ? 6 : 0;

        return score;
    }

    private static double ScorePartialBuild(BuildConfig build, PcBuildGeminiSuggestRequest request, BudgetRange budget)
    {
        var usage = (request.Usage ?? string.Empty).ToLowerInvariant();
        var perfTags = request.PerformanceTags?.Select(x => x.ToLowerInvariant()).ToList() ?? new List<string>();
        var budgetTarget = (double)budget.Target;

        var score = 0.0;
        var total = (double)build.TotalPrice;
        var diffRatio = budgetTarget <= 0 ? 0 : Math.Abs(total - budgetTarget) / budgetTarget;
        score += Math.Max(0, 36 - diffRatio * 85);

        if (build.TotalPrice >= budget.Min && (budget.Max <= 0 || build.TotalPrice <= budget.Max))
            score += 16;

        if (build.Cpu is not null && !string.IsNullOrWhiteSpace(build.Cpu.Socket)) score += 3;
        if (build.Motherboard is not null && !string.IsNullOrWhiteSpace(build.Motherboard.Socket)) score += 3;
        if (build.Ram is not null && !string.IsNullOrWhiteSpace(build.Ram.Type)) score += 2;

        var gpuPower = build.Gpu?.PowerConsumption ?? 0;
        var gpuVram = build.Gpu?.Vram ?? 0;
        var cpuCores = build.Cpu?.Cores ?? 0;
        var ramCap = build.Ram?.Capacity ?? 0;
        var storageCap = build.Storage?.Capacity ?? 0;

        if (usage.Contains("gaming") || perfTags.Any(x => x.Contains("fps")))
            score += gpuVram * 1.6 + gpuPower * 0.05 + cpuCores * 0.7;

        if (usage.Contains("design") || usage.Contains("video") || perfTags.Any(x => x.Contains("multi")))
            score += cpuCores * 1.2 + ramCap * 0.7 + storageCap * 0.02;

        if (usage.Contains("ai") || usage.Contains("program") || usage.Contains("dev"))
            score += cpuCores * 1.4 + ramCap * 0.8 + gpuVram * 0.9;

        return score;
    }

    private static IEnumerable<CaseDto?> FilterCasesByMotherboard(IEnumerable<CaseDto?> cases, MotherboardDto motherboard)
    {
        var mbForm = NormalizeFormFactor(motherboard.FormFactor);
        return cases.Where(c =>
        {
            if (c is null)
                return true;

            var supported = (c.SupportedFormFactors ?? new List<string>())
                .Select(NormalizeFormFactor)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (supported.Count == 0)
                return string.Equals(NormalizeFormFactor(c.FormFactor), mbForm, StringComparison.OrdinalIgnoreCase);
            return supported.Any(x => string.Equals(x, mbForm, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static IEnumerable<CoolingDto?> FilterCoolingsByCpu(IEnumerable<CoolingDto?> coolings, CpuDto cpu)
    {
        var cpuSocket = NormalizeSocket(cpu.Socket);
        return coolings.Where(cooling =>
        {
            if (cooling is null)
                return true;

            if (cooling.TdpSupport > 0 && cpu.Tdp > 0 && cooling.TdpSupport + 10 < cpu.Tdp)
                return false;

            if (cooling.SupportedSockets is null || cooling.SupportedSockets.Count == 0)
                return true;

            return cooling.SupportedSockets
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeSocket)
                .Any(s => string.Equals(s, cpuSocket, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static bool IsPartialBudgetAcceptable(BuildConfig build, BudgetRange budget)
    {
        if (budget.Max <= 0)
            return true;
        return build.TotalPrice <= budget.Max * 1.10m;
    }

    private static bool IsFinalBudgetAcceptable(BuildConfig build, BudgetRange budget)
    {
        if (budget.Max > 0 && build.TotalPrice > budget.Max)
            return false;
        if (budget.Min > 0 && build.TotalPrice < budget.Min * 0.85m)
            return false;
        return true;
    }

    private static bool CanCompleteWithinBudget(BuildConfig partial, BuildStage stage, BudgetRange budget, BuildStageCostModel costModel)
    {
        if (budget.Max <= 0)
            return true;

        var minRemaining = GetMinRemainingCost(stage, costModel);
        if (minRemaining < 0)
            return false;

        return partial.TotalPrice + minRemaining <= budget.Max;
    }

    private static decimal GetMinRemainingCost(BuildStage stage, BuildStageCostModel costModel)
    {
        var unknown = -1m;
        return stage switch
        {
            BuildStage.CpuMotherboard => SumKnown(costModel.MinRam, costModel.MinStorage, costModel.MinGpuNullable, costModel.MinPsu, costModel.MinCaseNullable, costModel.MinCoolingNullable),
            BuildStage.Ram => SumKnown(costModel.MinStorage, costModel.MinGpuNullable, costModel.MinPsu, costModel.MinCaseNullable, costModel.MinCoolingNullable),
            BuildStage.Storage => SumKnown(costModel.MinGpuNullable, costModel.MinPsu, costModel.MinCaseNullable, costModel.MinCoolingNullable),
            BuildStage.Gpu => SumKnown(costModel.MinPsu, costModel.MinCaseNullable, costModel.MinCoolingNullable),
            BuildStage.Psu => SumKnown(costModel.MinCaseNullable, costModel.MinCoolingNullable),
            BuildStage.Case => SumKnown(costModel.MinCoolingNullable),
            BuildStage.Cooling => 0m,
            _ => unknown
        };
    }

    private static decimal SumKnown(params decimal[] values)
    {
        decimal sum = 0;
        foreach (var v in values)
        {
            if (v < 0)
                return -1m;
            sum += v;
        }
        return sum;
    }

    private static BuildStageCostModel CreateBuildStageCostModel(PcComponentsCatalogDto filtered)
    {
        return new BuildStageCostModel(
            MinRam: MinPriceOrUnknown(filtered.Rams),
            MinStorage: MinPriceOrUnknown(filtered.Storages),
            MinGpuNullable: MinPriceOrZero(filtered.Gpus),
            MinPsu: MinPriceOrUnknown(filtered.Psus),
            MinCaseNullable: MinPriceOrZero(filtered.Cases),
            MinCoolingNullable: MinPriceOrZero(filtered.Coolings));
    }

    private static decimal MinPriceOrUnknown<T>(IEnumerable<T> items) where T : BaseComponentDto
    {
        var list = items.ToList();
        return list.Count == 0 ? -1m : list.Min(x => x.Price);
    }

    private static decimal MinPriceOrZero<T>(IEnumerable<T> items) where T : BaseComponentDto
    {
        var list = items.ToList();
        return list.Count == 0 ? 0m : list.Min(x => x.Price);
    }

    private static string NormalizeConnector(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return value.Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
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

    private static BuildConfig? BuildBestEffortCompatibleBuild(PcComponentsCatalogDto catalog, PcBuildGeminiSuggestRequest request, BudgetRange budget)
    {
        var fallbackRules = BuildRuleProfile.MostRelaxed with { PsuMaxMultiplier = 0 };
        var filtered = ApplyDependencyAwareFiltering(catalog, request, 40, fallbackRules);
        var builds = GenerateCompatibleBuildsBeamSearch(filtered, request, budget, 260, 20, fallbackRules);
        return builds.OrderByDescending(x => ScoreBuild(x, request, budget)).ThenBy(x => x.TotalPrice).FirstOrDefault();
    }

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
    private enum BuildStage
    {
        CpuMotherboard,
        Ram,
        Storage,
        Gpu,
        Psu,
        Case,
        Cooling
    }

    private readonly record struct BuildStageCostModel(
        decimal MinRam,
        decimal MinStorage,
        decimal MinGpuNullable,
        decimal MinPsu,
        decimal MinCaseNullable,
        decimal MinCoolingNullable);
}
