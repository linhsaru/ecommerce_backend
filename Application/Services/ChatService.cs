using Application.Common;
using Application.DTOs.Chat;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Application.Services;

public sealed class ChatService : IChatService
{
    private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const string DefaultModel = "models/gemini-2.5-flash-lite";
    private const string ProductsCacheKey = "botchat:products:v2";
    private const string SearchStateKeyPrefix = "botchat:searchstate:v1";
    private const int MaxToolRounds = 8;
    private static readonly string[] SearchDatasetSuffixes = ["laptop", "pc", "all"];
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
    private const string DefaultSystemPrompt =
        "Bạn là trợ lý tư vấn sản phẩm của cửa hàng.\n" +
        "- Khi cần thông tin sản phẩm (gợi ý, so sánh, tìm theo nhu cầu/ngân sách), bạn PHẢI gọi công cụ search_products với query phù hợp; có thể kèm budget (VND) nếu người dùng nêu giá.\n" +
        "- Khi người dùng muốn xem thêm kết quả đã tìm (xem thêm, tiếp, next, load more), gọi get_more_products.\n" +
        "- Chỉ mô tả sản phẩm dựa trên trường snippet (và các số totalMatched/returned/hasMore) trong kết quả công cụ. Không bịa tên, giá hay thông số không có trong snippet.\n" +
        "- Nếu snippet rỗng hoặc không có mặt hàng phù hợp, nói rõ là không tìm thấy.\n" +
        "- Trả lời ngắn gọn, thân thiện, dễ hiểu.";
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
        var modelToUse = IsAllowedModel(requestedModel) ? requestedModel! : normalizedDefaultModel ?? DefaultModel;
        if (!IsAllowedModel(modelToUse))
        {
            modelToUse = AllowedModels.Contains(DefaultModel) ? DefaultModel : AllowedModels.First();
        }
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString("N")
            : request.SessionId.Trim();
        int? lastTotal = null;
        int? lastReturned = null;
        bool? lastHasMore = null;
        var contents = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = request.Message.Trim() }
                }
            }
        };
        string? lastRaw = null;
        for (var round = 0; round < MaxToolRounds; round++)
        {
            var url = $"{GeminiUrl}{modelToUse}:generateContent?key={apiKey}";
            var body = BuildGenerateContentBody(contents);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = new StringContent(
                body.ToJsonString(),
                Encoding.UTF8,
                "application/json");
            using var httpResponse = await client.SendAsync(httpRequest, cancellationToken);
            var raw = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            lastRaw = raw;
            if (!httpResponse.IsSuccessStatusCode)
            {
                return Result<ChatResponse>.Fail(
                    "GEMINI_ERROR",
                    "Gemini request failed.",
                    $"Model={modelToUse}. Raw={raw}");
            }
            JsonElement root;
            try
            {
                root = JsonSerializer.Deserialize<JsonElement>(raw);
            }
            catch (JsonException)
            {
                return Result<ChatResponse>.Fail("GEMINI_ERROR", "Failed to parse Gemini response.", raw);
            }
            if (!root.TryGetProperty("candidates", out var candidatesEl) ||
                candidatesEl.GetArrayLength() == 0)
            {
                return Result<ChatResponse>.Fail("GEMINI_ERROR", "Gemini returned no candidates.", raw);
            }
            var candidate = candidatesEl[0];
            if (!candidate.TryGetProperty("content", out var contentEl))
            {
                return Result<ChatResponse>.Fail("GEMINI_ERROR", "Gemini candidate has no content.", raw);
            }
            if (!contentEl.TryGetProperty("parts", out var partsEl))
            {
                return Result<ChatResponse>.Fail("GEMINI_ERROR", "Gemini content has no parts.", raw);
            }
            var pendingCalls = new List<PendingFunctionCall>();
            foreach (var part in partsEl.EnumerateArray())
            {
                if (!part.TryGetProperty("functionCall", out var fc))
                    continue;
                var name = fc.TryGetProperty("name", out var nameP) ? nameP.GetString() ?? "" : "";
                var id = fc.TryGetProperty("id", out var idP) ? idP.GetString() : null;
                JsonElement args = default;
                if (fc.TryGetProperty("args", out var argsP))
                    args = argsP;
                if (!string.IsNullOrWhiteSpace(name))
                    pendingCalls.Add(new PendingFunctionCall(name, args, id));
            }
            if (pendingCalls.Count == 0)
            {
                var reply = ConcatenateTextParts(partsEl);
                if (string.IsNullOrWhiteSpace(reply))
                    return Result<ChatResponse>.Fail("GEMINI_ERROR", "Gemini returned an empty reply.", raw);
                return Result<ChatResponse>.Ok(new ChatResponse
                {
                    Reply = reply.Trim(),
                    ModelUsed = modelToUse,
                    SessionId = sessionId,
                    TotalMatchedProducts = lastTotal,
                    ReturnedProducts = lastReturned,
                    HasMoreProducts = lastHasMore
                });
            }
            contents.Add(JsonNode.Parse(contentEl.GetRawText())!);
            var responseParts = new JsonArray();
            foreach (var call in pendingCalls)
            {
                JsonObject responsePayload;
                if (call.Name.Equals("search_products", StringComparison.OrdinalIgnoreCase))
                {
                    var query = TryGetStringArg(call.Args, "query") ?? "";
                    var budget = TryGetDecimalArg(call.Args, "budget");
                    var searchMessage = MergeToolQueryWithUserMessage(query, request.Message);
                    var relevant = await GetRelevantProductsForPromptAsync(
                        searchMessage,
                        sessionId,
                        request.PageSize,
                        budget,
                        cancellationToken);
                    sessionId = relevant.SessionId;
                    lastTotal = relevant.TotalMatched;
                    lastReturned = relevant.Returned;
                    lastHasMore = relevant.HasMore;
                    responsePayload = new JsonObject
                    {
                        ["snippet"] = relevant.ProductsData,
                        ["totalMatched"] = relevant.TotalMatched,
                        ["returned"] = relevant.Returned,
                        ["hasMore"] = relevant.HasMore,
                        ["sessionId"] = relevant.SessionId
                    };
                }
                else if (call.Name.Equals("get_more_products", StringComparison.OrdinalIgnoreCase))
                {
                    var more = await TryGetMoreProductsPageAsync(sessionId, cancellationToken);
                    if (more is null)
                    {
                        responsePayload = new JsonObject
                        {
                            ["error"] = "Không có trang kết quả trước đó. Hãy gọi search_products trước."
                        };
                    }
                    else
                    {
                        sessionId = more.Value.SessionId;
                        lastTotal = more.Value.TotalMatched;
                        lastReturned = more.Value.Returned;
                        lastHasMore = more.Value.HasMore;
                        responsePayload = new JsonObject
                        {
                            ["snippet"] = more.Value.ProductsData,
                            ["totalMatched"] = more.Value.TotalMatched,
                            ["returned"] = more.Value.Returned,
                            ["hasMore"] = more.Value.HasMore,
                            ["sessionId"] = more.Value.SessionId
                        };
                    }
                }
                else
                {
                    responsePayload = new JsonObject
                    {
                        ["error"] = $"Unknown function: {call.Name}"
                    };
                }
                var fr = new JsonObject
                {
                    ["name"] = call.Name,
                    ["response"] = responsePayload
                };
                if (!string.IsNullOrEmpty(call.Id))
                    fr["id"] = call.Id;
                responseParts.Add(new JsonObject { ["functionResponse"] = fr });
            }
            contents.Add(new JsonObject
            {
                ["role"] = "tool",
                ["parts"] = responseParts
            });
        }
        return Result<ChatResponse>.Fail(
            "GEMINI_ERROR",
            "Exceeded maximum tool round-trips.",
            lastRaw ?? "");
    }
    private readonly record struct PendingFunctionCall(string Name, JsonElement Args, string? Id);
    private static JsonObject BuildGenerateContentBody(JsonArray contents)
    {
        var contentsClone = JsonNode.Parse(contents.ToJsonString())!.AsArray();
        return new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = DefaultSystemPrompt } }
            },
            ["contents"] = contentsClone,
            ["tools"] = BuildTools(),
            ["toolConfig"] = new JsonObject
            {
                ["functionCallingConfig"] = new JsonObject { ["mode"] = "AUTO" }
            }
        };
    }
    private static JsonArray BuildTools()
    {
        return new JsonArray
        {
            new JsonObject
            {
                ["functionDeclarations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "search_products",
                        ["description"] = "Tìm sản phẩm phù hợp với nhu cầu người dùng trong kho cửa hàng.",
                        ["parameters"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["query"] = new JsonObject
                                {
                                    ["type"] = "string",
                                    ["description"] = "Từ khóa / mô tả tìm kiếm (ví dụ: laptop văn phòng, RAM 16GB, RTX 4060)."
                                },
                                ["budget"] = new JsonObject
                                {
                                    ["type"] = "number",
                                    ["description"] = "Ngân sách tối đa (VND), nếu người dùng có nêu."
                                }
                            },
                            ["required"] = new JsonArray { "query" }
                        }
                    },
                    new JsonObject
                    {
                        ["name"] = "get_more_products",
                        ["description"] =
                            "Lấy trang kết quả tiếp theo (phân trang) sau khi đã search_products, khi người dùng muốn xem thêm.",
                        ["parameters"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject()
                        }
                    }
                }
            }
        };
    }
    private static string MergeToolQueryWithUserMessage(string toolQuery, string userMessage)
    {
        var q = (toolQuery ?? "").Trim();
        var u = (userMessage ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q)) return u;
        if (string.IsNullOrWhiteSpace(u)) return q;
        return $"{q}\n{u}";
    }
    private static string? TryGetStringArg(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }
    private static decimal? TryGetDecimalArg(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.Number => p.GetDecimal(),
            JsonValueKind.String => decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null,
            _ => null
        };
    }
    private static string ConcatenateTextParts(JsonElement partsEl)
    {
        var sb = new StringBuilder();
        foreach (var part in partsEl.EnumerateArray())
        {
            if (!part.TryGetProperty("text", out var textP))
                continue;
            var t = textP.GetString();
            if (string.IsNullOrWhiteSpace(t))
                continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(t.Trim());
        }
        return sb.ToString();
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
    private static (decimal? Min, decimal? Max) MergePriceRange((decimal? Min, decimal? Max) fromText, decimal? toolMaxVnd)
    {
        if (toolMaxVnd is null || toolMaxVnd <= 0)
            return fromText;
        var cap = toolMaxVnd.Value;
        var max = fromText.Max is null ? cap : Math.Min(fromText.Max.Value, cap);
        var min = fromText.Min;
        if (min is not null && max < min)
            max = cap;
        return (min, max);
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
    private async Task<SearchState?> FindSearchStateForSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        foreach (var suffix in SearchDatasetSuffixes)
        {
            var key = $"{SearchStateKeyPrefix}:{sessionId}:{suffix}";
            var s = await _cacheService.GetAsync<SearchState>(key);
            if (s is { MatchedSlugs.Count: > 0 })
                return s;
        }
        return null;
    }
    private async Task<(string ProductsData, int TotalMatched, int Returned, bool HasMore, string SessionId)?> TryGetMoreProductsPageAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var prev = await FindSearchStateForSessionAsync(sessionId, cancellationToken);
        if (prev is null || prev.MatchedSlugs.Count == 0)
            return null;
        var dataset = new ProductsDataset(prev.DatasetKey, ResolveCategorySlugsForDatasetKey(prev.DatasetKey));
        var full = await GetOrBuildFullDatasetAsync(dataset, cancellationToken);
        var bySlug = full.ToDictionary(x => x.Slug, StringComparer.OrdinalIgnoreCase);
        var cursor = Math.Max(0, prev.Cursor);
        var pageSize = prev.PageSize;
        var sliceSlugs = prev.MatchedSlugs.Skip(cursor).Take(pageSize).ToList();
        var slice = sliceSlugs
            .Select(s => bySlug.GetValueOrDefault(s))
            .Where(x => x is not null)
            .Cast<ProductIndexItem>()
            .ToList();
        var nextCursor = cursor + sliceSlugs.Count;
        var hasMore = nextCursor < prev.MatchedSlugs.Count;
        var updated = prev with { Cursor = nextCursor };
        var expiryMinutes = ResolveSessionExpiryMinutes();
        var stateKey = $"{SearchStateKeyPrefix}:{sessionId}:{prev.DatasetKey}";
        await _cacheService.SetAsync(stateKey, updated, TimeSpan.FromMinutes(expiryMinutes));
        var data = BuildProductsSnippet(slice, prev.MatchedSlugs.Count, cursor, pageSize);
        return (data, prev.MatchedSlugs.Count, slice.Count, hasMore, sessionId);
    }
    private static string[] ResolveCategorySlugsForDatasetKey(string datasetKey)
        => datasetKey switch
        {
            "laptop" => [LaptopCategorySlug],
            "pc" => PcCategorySlugs,
            _ => Array.Empty<string>()
        };
    private async Task<(string ProductsData, int TotalMatched, int Returned, bool HasMore, string SessionId)> GetRelevantProductsForPromptAsync(
        string message,
        string? sessionId,
        int? pageSizeOverride,
        decimal? toolBudgetMaxVnd,
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
            var prev = await FindSearchStateForSessionAsync(sid, cancellationToken);
            if (prev is not null && prev.MatchedSlugs.Count > 0)
            {
                var full = await GetOrBuildFullDatasetAsync(
                    new ProductsDataset(prev.DatasetKey, ResolveCategorySlugsForDatasetKey(prev.DatasetKey)),
                    cancellationToken);
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
                var sk = $"{SearchStateKeyPrefix}:{sid}:{prev.DatasetKey}";
                await _cacheService.SetAsync(sk, updated, TimeSpan.FromMinutes(expiryMinutes));
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
        var safeMessage = message ?? "";
        var price = MergePriceRange(ExtractPriceRangeVnd(safeMessage), toolBudgetMaxVnd);
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
            ComputeQueryHash(datasetKey, safeMessage),
            matchedSlugs,
            Cursor: slice2.Count,
            PageSize: pageSize,
            CreatedAt: DateTimeOffset.UtcNow);
        await _cacheService.SetAsync(stateKey, newState, TimeSpan.FromMinutes(expiryMinutes));
        var data2 = BuildProductsSnippet(slice2, matched.Count, 0, pageSize);
        return (data2, matched.Count, slice2.Count, hasMore2, sid);
    }
    private readonly record struct ProductsDataset(string CacheKeySuffix, string[] CategorySlugs);
    private static ProductsDataset DetectDataset(string? message)
    {
        var text = (message ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return new ProductsDataset("all", Array.Empty<string>());
        var lower = text.ToLowerInvariant();
        if (lower.Contains("laptop"))
            return new ProductsDataset("laptop", [LaptopCategorySlug]);
        if (lower.Contains("xây dựng cấu hình") ||
            lower.Contains("cấu hình máy tính") ||
            lower.Contains("build pc") ||
            lower.Contains("pc"))
        {
            return new ProductsDataset("pc", PcCategorySlugs);
        }
        return new ProductsDataset("all", Array.Empty<string>());
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
}
