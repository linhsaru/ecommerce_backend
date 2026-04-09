using Application.Common;
using Application.DTOs.Chat;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Application.Services;

public sealed class ChatService : IChatService
{
    private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const string DefaultModel = "models/gemini-2.5-flash-lite";
    private const string DefaultSystemPrompt = "Bạn là AI tư vấn sản phẩm.";
    private const string ProductsCacheKey = "botchat:products:v2";
    private const string SearchStateKeyPrefix = "botchat:searchstate:v1";
    private static readonly string[] PcCategorySlugs =
    [
        "linh-kien-may-tinh",
        "tan-nhiet",
        "thiet-bi-luu-tru"
    ];
    private const string LaptopCategorySlug = "laptop";

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
    private readonly ICacheService _cacheService;
    private readonly IProductRepository _productRepository;

    public ChatService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ICacheService cacheService,
        IProductRepository productRepository)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _cacheService = cacheService;
        _productRepository = productRepository;
    }

    public async Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
            return Result<ChatResponse>.Fail("VALIDATION_ERROR", "Message is required.");

        var apiKey = _configuration["BotChat:KeyBot"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result<ChatResponse>.Fail("CONFIG_ERROR", "Gemini API key is missing (BotChat:KeyBot).");

        var client = _httpClientFactory.CreateClient("Gemini");

        var requestedModelRaw = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model!.Trim();
        var defaultModelFromConfig = _configuration["BotChat:DefaultModel"]?.Trim();

        var defaultModel = string.IsNullOrWhiteSpace(defaultModelFromConfig) ? DefaultModel : defaultModelFromConfig;
        var requestedModel = NormalizeModelName(requestedModelRaw);
        var normalizedDefaultModel = NormalizeModelName(defaultModel);

        // Only allow models from the provided list; fallback to default if request is not allowed.
        var modelToUse = IsAllowedModel(requestedModel) ? requestedModel! : normalizedDefaultModel ?? DefaultModel;
        if (!IsAllowedModel(modelToUse))
        {
            modelToUse = AllowedModels.Contains(DefaultModel) ? DefaultModel : AllowedModels.First();
        }

        string? lastRaw = null;
        string? lastModelUsed = null;

        foreach (var model in new[] { modelToUse })
        {
            lastModelUsed = model;

            var relevant = await GetRelevantProductsForPromptAsync(
                request.Message,
                request.SessionId,
                request.PageSize,
                cancellationToken);

            var userPrompt = BuildUserPrompt(relevant.ProductsData, request.Message);

            var payload = new
            {
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = DefaultSystemPrompt }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new
                            {
                                text = userPrompt
                            }
                        }
                    }
                }
            };

            var url = $"{GeminiUrl}{model}:generateContent?key={apiKey}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = JsonContent.Create(payload);

            using var httpResponse = await client.SendAsync(httpRequest, cancellationToken);
            var raw = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            lastRaw = raw;

            if (!httpResponse.IsSuccessStatusCode)
            {
                if (raw.Contains("model", StringComparison.OrdinalIgnoreCase) &&
                    (raw.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                     raw.Contains("invalid", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return Result<ChatResponse>.Fail(
                    "GEMINI_ERROR",
                    "Gemini request failed.",
                    $"Model={model}. Raw={raw}");
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(
                    raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var reply = parsed?.Candidates?
                    .FirstOrDefault()?
                    .Content?
                    .Parts?
                    .Select(p => p.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Aggregate("", (acc, cur) => string.IsNullOrEmpty(acc) ? cur!.Trim() : $"{acc}\n{cur!.Trim()}");

                if (string.IsNullOrWhiteSpace(reply))
                    return Result<ChatResponse>.Fail("GEMINI_ERROR", "Gemini returned an empty reply.", raw);

                return Result<ChatResponse>.Ok(new ChatResponse
                {
                    Reply = reply.Trim(),
                    ModelUsed = model,
                    SessionId = relevant.SessionId,
                    TotalMatchedProducts = relevant.TotalMatched,
                    ReturnedProducts = relevant.Returned,
                    HasMoreProducts = relevant.HasMore
                });
            }
            catch (JsonException)
            {
                return Result<ChatResponse>.Fail("GEMINI_ERROR", "Failed to parse Gemini response.", raw);
            }
        }

        return Result<ChatResponse>.Fail(
            "GEMINI_ERROR",
            "Gemini request failed (invalid/unsupported model).",
            lastRaw ?? $"Model tried={lastModelUsed}");
    }

    private sealed record ProductIndexItem(
        string Name,
        string Slug,
        string? BrandName,
        int Status,
        string? ThumbnailUrl,
        List<string> CategorySlugs,
        int VariantCount,
        decimal? MinPrice,
        decimal? MaxPrice,
        List<string> Specs);

    private sealed record SearchState(
        string DatasetKey,
        string QueryHash,
        List<string> MatchedSlugs,
        int Cursor,
        int PageSize,
        DateTimeOffset CreatedAt);

    private async Task<List<ProductIndexItem>> GetOrBuildFullDatasetAsync(ProductsDataset dataset, CancellationToken cancellationToken)
    {
        var cacheKey = $"{ProductsCacheKey}:{dataset.CacheKeySuffix}:fullindex";
        var cached = await _cacheService.GetAsync<List<ProductIndexItem>>(cacheKey);
        if (cached is { Count: > 0 })
            return cached;

        var query = _productRepository
            .GetQueryable()
            .Include(p => p.Brand)
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductVariants).ThenInclude(v => v.ProductVariantSpecifications).ThenInclude(s => s.SpecificationType)
            .AsQueryable();

        if (dataset.CategorySlugs.Length > 0)
        {
            var slugs = dataset.CategorySlugs;
            query = query.Where(p => p.ProductCategories.Any(pc => slugs.Contains(pc.Category.Slug)));
        }

        var products = await query
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var items = products.Select(p =>
        {
            var categories = p.ProductCategories
                .Select(pc => pc.Category?.Slug)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            var variants = p.ProductVariants ?? new List<Domain.Entities.ProductVariant>();
            var variantCount = variants.Count;
            decimal? minPrice = null;
            decimal? maxPrice = null;
            if (variantCount > 0)
            {
                minPrice = variants.Min(v => v.Price);
                maxPrice = variants.Max(v => v.Price);
            }

            var specs = variants
                .SelectMany(v => v.ProductVariantSpecifications ?? new List<Domain.Entities.ProductVariantSpecification>())
                .Where(s => s.SpecificationType != null && !string.IsNullOrWhiteSpace(s.Value))
                .Select(s => $"{s.SpecificationType.Name}{(string.IsNullOrWhiteSpace(s.SpecificationType.Unit) ? "" : $" ({s.SpecificationType.Unit})")}: {s.Value}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToList();

            return new ProductIndexItem(
                p.Name,
                p.Slug,
                p.Brand?.Name,
                p.Status,
                p.ThumbnailUrl,
                categories,
                variantCount,
                minPrice,
                maxPrice,
                specs);
        }).ToList();

        var expiryMinutes = 60;
        if (int.TryParse(_configuration["BotChat:ProductsCacheExpiryMinutes"], out var minutes) && minutes > 0)
            expiryMinutes = minutes;

        await _cacheService.SetAsync(cacheKey, items, TimeSpan.FromMinutes(expiryMinutes));
        return items;
    }

    private static bool IsShowMoreMessage(string message)
    {
        var lower = (message ?? "").Trim().ToLowerInvariant();
        return lower is "xem thêm" or "xem them" or "thêm" or "them" or "xem tiếp" or "xem tiep" or "next" or "more";
    }

    private static string ComputeQueryHash(string datasetKey, string message)
    {
        var normalized = (message ?? "").Trim().ToLowerInvariant();
        return $"{datasetKey}:{normalized.GetHashCode()}";
    }

    private int ResolvePageSize(int? requested)
    {
        var defaultSize = 80;
        if (int.TryParse(_configuration["BotChat:SearchPageSize"], out var cfg) && cfg > 0) defaultSize = cfg;

        var size = requested ?? defaultSize;
        if (size < 20) size = 20;
        if (size > 200) size = 200;
        return size;
    }

    private int ResolveSessionExpiryMinutes()
    {
        var expiry = 30;
        if (int.TryParse(_configuration["BotChat:SessionExpiryMinutes"], out var cfg) && cfg > 0) expiry = cfg;
        return expiry;
    }

    private static string BuildProductsSnippet(List<ProductIndexItem> items, int totalMatched, int cursor, int pageSize)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Số sản phẩm phù hợp: {totalMatched}");
        sb.AppendLine($"Đang hiển thị: {Math.Min(totalMatched, cursor + items.Count)} / {totalMatched} (mỗi lần {pageSize})");
        sb.AppendLine();

        foreach (var p in items)
        {
            sb.Append("- ");
            sb.Append(p.Name);
            if (!string.IsNullOrWhiteSpace(p.BrandName))
                sb.Append($" | Hãng: {p.BrandName}");
            if (!string.IsNullOrWhiteSpace(p.Slug))
                sb.Append($" | Slug: {p.Slug}");
            if (p.CategorySlugs is { Count: > 0 })
                sb.Append($" | Danh mục: {string.Join(", ", p.CategorySlugs)}");
            if (p.MinPrice is not null || p.MaxPrice is not null)
                sb.Append($" | Giá: {p.MinPrice?.ToString() ?? "?"} - {p.MaxPrice?.ToString() ?? "?"}");
            if (p.Specs is { Count: > 0 })
                sb.Append($" | Thông số: {string.Join("; ", p.Specs)}");
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private static (decimal? Min, decimal? Max) ExtractPriceRangeVnd(string message)
    {
        var text = (message ?? "").ToLowerInvariant();

        static decimal ToVnd(decimal n, string unit)
        {
            unit = unit.ToLowerInvariant();
            if (unit.Contains("triệu") || unit.Contains("trieu")) return n * 1_000_000m;
            if (unit.Contains("k") || unit.Contains("nghìn") || unit.Contains("nghin")) return n * 1_000m;
            return n;
        }

        var under = Regex.Match(text, @"(dưới|duoi)\s*(\d+(?:[.,]\d+)?)\s*(triệu|trieu|k|nghìn|nghin)?");
        if (under.Success)
        {
            var n = decimal.Parse(under.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var unit = under.Groups[3].Success ? under.Groups[3].Value : "triệu";
            return (null, ToVnd(n, unit));
        }

        var between = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*(?:-|đến|den)\s*(\d+(?:[.,]\d+)?)\s*(triệu|trieu|k|nghìn|nghin)?");
        if (between.Success)
        {
            var a = decimal.Parse(between.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var b = decimal.Parse(between.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var unit = between.Groups[3].Success ? between.Groups[3].Value : "triệu";
            var min = ToVnd(Math.Min(a, b), unit);
            var max = ToVnd(Math.Max(a, b), unit);
            return (min, max);
        }

        return (null, null);
    }

    private static int ScoreProduct(ProductIndexItem p, HashSet<string> tokens, (decimal? Min, decimal? Max) price)
    {
        var score = 0;
        var nameLower = p.Name.ToLowerInvariant();
        var brandLower = (p.BrandName ?? "").ToLowerInvariant();

        foreach (var t in tokens)
        {
            if (t.Length < 2) continue;
            if (nameLower.Contains(t)) score += 8;
            if (!string.IsNullOrWhiteSpace(brandLower) && brandLower.Contains(t)) score += 10;
            if (p.CategorySlugs.Any(c => c.ToLowerInvariant().Contains(t))) score += 4;
        }

        if (price.Min is not null || price.Max is not null)
        {
            var min = p.MinPrice ?? p.MaxPrice;
            var max = p.MaxPrice ?? p.MinPrice;
            if (min is not null && max is not null)
            {
                var ok = true;
                if (price.Min is not null && max < price.Min) ok = false;
                if (price.Max is not null && min > price.Max) ok = false;
                score += ok ? 6 : -3;
            }
        }

        if (p.Status == 1) score += 1;
        if (p.VariantCount > 0) score += 1;
        return score;
    }

    private async Task<(string ProductsData, int TotalMatched, int Returned, bool HasMore, string SessionId)> GetRelevantProductsForPromptAsync(
        string message,
        string? sessionId,
        int? pageSizeOverride,
        CancellationToken cancellationToken)
    {
        var dataset = DetectDataset(message);
        var datasetKey = dataset.CacheKeySuffix;

        var pageSize = ResolvePageSize(pageSizeOverride);
        var expiryMinutes = ResolveSessionExpiryMinutes();

        var sid = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId!.Trim();
        var stateKey = $"{SearchStateKeyPrefix}:{sid}:{datasetKey}";

        if (IsShowMoreMessage(message))
        {
            var prev = await _cacheService.GetAsync<SearchState>(stateKey);
            if (prev is not null && prev.MatchedSlugs.Count > 0)
            {
                var full = await GetOrBuildFullDatasetAsync(dataset, cancellationToken);
                var bySlug = full.ToDictionary(x => x.Slug, StringComparer.OrdinalIgnoreCase);

                var cursor = Math.Max(0, prev.Cursor);
                var sliceSlugs = prev.MatchedSlugs.Skip(cursor).Take(prev.PageSize).ToList();
                var slice = sliceSlugs
                    .Select(s => bySlug.GetValueOrDefault(s))
                    .Where(x => x is not null)
                    .Cast<ProductIndexItem>()
                    .ToList();

                var nextCursor = cursor + sliceSlugs.Count;
                var hasMore = nextCursor < prev.MatchedSlugs.Count;

                var updated = prev with { Cursor = nextCursor };
                await _cacheService.SetAsync(stateKey, updated, TimeSpan.FromMinutes(expiryMinutes));

                var data = BuildProductsSnippet(slice, prev.MatchedSlugs.Count, cursor, prev.PageSize);
                return (data, prev.MatchedSlugs.Count, slice.Count, hasMore, sid);
            }
        }

        var fullDataset = await GetOrBuildFullDatasetAsync(dataset, cancellationToken);
        var lower = (message ?? "").Trim().ToLowerInvariant();
        var tokens = lower
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '/', '\\', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .Take(30)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var price = ExtractPriceRangeVnd(message);

        var matched = fullDataset
            .Select(p => new { p, score = ScoreProduct(p, tokens, price) })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.p.Name)
            .Select(x => x.p)
            .ToList();

        var matchedSlugs = matched.Select(x => x.Slug).ToList();
        var slice2 = matched.Take(pageSize).ToList();
        var hasMore2 = matched.Count > pageSize;

        var newState = new SearchState(
            datasetKey,
            ComputeQueryHash(datasetKey, message),
            matchedSlugs,
            Cursor: slice2.Count,
            PageSize: pageSize,
            CreatedAt: DateTimeOffset.UtcNow);

        await _cacheService.SetAsync(stateKey, newState, TimeSpan.FromMinutes(expiryMinutes));

        var data2 = BuildProductsSnippet(slice2, matched.Count, 0, pageSize);
        return (data2, matched.Count, slice2.Count, hasMore2, sid);
    }

    private async Task<string> GetProductsDataForPromptAsync(string message, CancellationToken cancellationToken)
    {
        // Backward-compat helper. New flow uses GetRelevantProductsForPromptAsync.
        var dataset = DetectDataset(message);
        var full = await GetOrBuildFullDatasetAsync(dataset, cancellationToken);
        var snippet = full.Take(50).ToList();
        return BuildProductsSnippet(snippet, snippet.Count, 0, 50);
    }

    private readonly record struct ProductsDataset(string CacheKeySuffix, string[] CategorySlugs);

    private static ProductsDataset DetectDataset(string? message)
    {
        var text = (message ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return new ProductsDataset("all", Array.Empty<string>());

        var lower = text.ToLowerInvariant();

        // Laptop intent
        if (lower.Contains("laptop"))
            return new ProductsDataset("laptop", [LaptopCategorySlug]);

        // PC build intent
        if (lower.Contains("xây dựng cấu hình") ||
            lower.Contains("cấu hình máy tính") ||
            lower.Contains("build pc") ||
            lower.Contains("pc"))
        {
            return new ProductsDataset("pc", PcCategorySlugs);
        }

        return new ProductsDataset("all", Array.Empty<string>());
    }

    private static string BuildUserPrompt(string productsData, string message)
    {
        return
            "Bạn là AI tư vấn sản phẩm.\n\n" +
            "Dữ liệu sản phẩm:\n" +
            productsData + "\n\n" +
            "Câu hỏi:\n" +
            (message ?? "").Trim() + "\n\n" +
            "Yêu cầu:\n" +
            "- Hãy trả lời ngắn gọn, giọng điệu thân thiện, dễ hiểu, không dùng từ chuyên môn khó\n" +
            "- Chỉ trả lời dựa trên dữ liệu\n" +
            "- Không bịa\n" +
            "- Nếu không có thì nói không có";
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
}

