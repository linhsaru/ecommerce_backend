using Application.Common;
using Application.DTOs.Chat;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
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
    private const decimal BudgetToleranceRatio = 0.12m;
    private static readonly string[] SearchDatasetSuffixes = ["laptop", "pc", "all"];
    private static readonly HashSet<string> SearchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "toi", "toi", "minh", "ban", "giup", "hay", "goi", "goi y", "tu van", "cho", "toi", "nhe",
        "can", "muon", "tim", "kiem", "san", "pham", "cau", "hinh", "may", "tinh", "duoc", "voi",
        "la", "nhu", "cau", "va", "hoac", "de", "phuc", "vu", "tam", "gia", "khoang", "duoi", "tren",
        "pc", "build", "mot", "nhung", "nhung", "giup", "them"
    };
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
    private static readonly string[] BuiltInFallbackModels =
    [
        "gemini-2.5-flash",
        "gemini-2.0-flash",
        "gemini-2.0-flash-lite",
        "gemini-2.5-pro"
    ];
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
            var body = BuildGenerateContentBody(contents);
            var chain = ResolveModelFallbackChain(modelToUse);
            string raw = "";
            var requestOk = false;
            foreach (var geminiModel in chain)
            {
                var url = $"{GeminiUrl}{geminiModel}:generateContent?key={apiKey}";
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Content = new StringContent(
                    body.ToJsonString(),
                    Encoding.UTF8,
                    "application/json");
                using var httpResponse = await client.SendAsync(httpRequest, cancellationToken);
                raw = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                lastRaw = raw;
                if (httpResponse.IsSuccessStatusCode)
                {
                    modelToUse = geminiModel;
                    requestOk = true;
                    break;
                }
                if (!IsTransientGeminiFailure(httpResponse.StatusCode, raw))
                {
                    return Result<ChatResponse>.Fail(
                        "GEMINI_ERROR",
                        "Gemini request failed.",
                        $"Model={geminiModel}. Raw={raw}");
                }
            }
            if (!requestOk)
            {
                return Result<ChatResponse>.Fail(
                    "GEMINI_ERROR",
                    "Gemini temporarily unavailable for all configured models. Try again later or change BotChat:DefaultModel / FallbackModels.",
                    $"Tried=[{string.Join(", ", chain)}]. Raw={raw}");
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
        decimal? RepresentativePrice,
        List<string> Specs,
        string SearchText);
    private readonly record struct SearchIntent(
        bool LaptopPreferred,
        bool PcPreferred,
        bool GamingPreferred,
        bool OfficePreferred);
    private readonly record struct BudgetHint(
        decimal? Min,
        decimal? Max,
        decimal? Target);
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
                .Take(12)
                .ToList();
            var representativePrice = minPrice is not null && maxPrice is not null
                ? Math.Round((minPrice.Value + maxPrice.Value) / 2m, 0)
                : minPrice ?? maxPrice;
            var searchText = NormalizeForSearch(
                $"{p.Name} {p.Brand?.Name} {string.Join(" ", categories)} {p.Description} {string.Join(" ", specs)}");
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
                representativePrice,
                specs,
                searchText);
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
        var defaultSize = 24;
        if (int.TryParse(_configuration["BotChat:SearchPageSize"], out var cfg) && cfg > 0) defaultSize = cfg;
        var size = requested ?? defaultSize;
        if (size < 10) size = 10;
        if (size > 80) size = 80;
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
                sb.Append($" | Thông số: {string.Join("; ", p.Specs.Take(6).Select(TrimForSnippet))}");
            sb.AppendLine();
        }
        return sb.ToString().Trim();
    }
    private static string TrimForSnippet(string? value, int maxLength = 80)
    {
        var text = (value ?? "").Trim();
        if (text.Length <= maxLength) return text;
        return $"{text[..maxLength].TrimEnd()}...";
    }
    private static BudgetHint ExtractBudgetHintVnd(string message)
    {
        var text = NormalizeForSearch(message);
        var hasBudgetCue = text.Contains("gia") ||
                           text.Contains("ngan sach") ||
                           text.Contains("duoi") ||
                           text.Contains("tren") ||
                           text.Contains("tam") ||
                           text.Contains("khoang") ||
                           text.Contains("budget");
        static decimal ToVnd(decimal n, string unit)
        {
            unit = unit.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(unit)) return n;
            if (unit.Contains("triệu") || unit.Contains("trieu")) return n * 1_000_000m;
            if (unit == "tr" || unit == "m") return n * 1_000_000m;
            if (unit.Contains("củ") || unit.Contains("cu")) return n * 1_000_000m;
            if (unit.Contains("k") || unit.Contains("nghìn") || unit.Contains("nghin")) return n * 1_000m;
            return n;
        }
        var under = Regex.Match(text, @"(duoi|<=|toi da|max)\s*(\d+(?:[.,]\d+)?)\s*(trieu|tr|m|cu|k|nghin)?");
        if (under.Success)
        {
            var n = decimal.Parse(under.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var unit = under.Groups[3].Success ? under.Groups[3].Value : "trieu";
            return new BudgetHint(null, ToVnd(n, unit), ToVnd(n, unit));
        }
        var over = Regex.Match(text, @"(tren|>=|tu)\s*(\d+(?:[.,]\d+)?)\s*(trieu|tr|m|cu|k|nghin)?");
        if (over.Success)
        {
            var n = decimal.Parse(over.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var unit = over.Groups[3].Success ? over.Groups[3].Value : "trieu";
            var min = ToVnd(n, unit);
            return new BudgetHint(min, null, min);
        }
        var between = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*(?:-|den|to)\s*(\d+(?:[.,]\d+)?)\s*(trieu|tr|m|cu|k|nghin)?");
        if (between.Success)
        {
            var a = decimal.Parse(between.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var b = decimal.Parse(between.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var unit = between.Groups[3].Success ? between.Groups[3].Value : "trieu";
            var min = ToVnd(Math.Min(a, b), unit);
            var max = ToVnd(Math.Max(a, b), unit);
            return new BudgetHint(min, max, Math.Round((min + max) / 2m, 0));
        }
        if (hasBudgetCue)
        {
            var single = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*(trieu|tr|m|cu|k|nghin)?");
            if (single.Success)
            {
                var n = decimal.Parse(single.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                var unit = single.Groups[2].Success ? single.Groups[2].Value : (n <= 300 ? "trieu" : "");
                var target = ToVnd(n, unit);
                if (target > 0)
                {
                    var delta = Math.Round(target * 0.15m, 0);
                    return new BudgetHint(Math.Max(0, target - delta), target + delta, target);
                }
            }
        }
        return new BudgetHint(null, null, null);
    }
    private static BudgetHint MergeBudgetHint(BudgetHint fromText, decimal? toolMaxVnd)
    {
        if (toolMaxVnd is null || toolMaxVnd <= 0)
            return fromText;
        var cap = toolMaxVnd.Value;
        var max = fromText.Max is null ? cap : Math.Min(fromText.Max.Value, cap);
        var min = fromText.Min;
        if (min is not null && max < min)
            max = cap;
        var target = fromText.Target is null ? cap : Math.Min(fromText.Target.Value, cap);
        return new BudgetHint(min, max, target);
    }
    private static SearchIntent DetectSearchIntent(string message, string datasetKey)
    {
        var text = NormalizeForSearch(message);
        var laptopPreferred = datasetKey == "laptop" || text.Contains("laptop");
        var pcPreferred = datasetKey == "pc" || text.Contains("pc") || text.Contains("cau hinh");
        var gamingPreferred = text.Contains("gaming") || text.Contains("choi game") || text.Contains("fps");
        var officePreferred = text.Contains("van phong") || text.Contains("hoc tap") || text.Contains("office");
        return new SearchIntent(laptopPreferred, pcPreferred, gamingPreferred, officePreferred);
    }
    private static bool IsBudgetCompatible(ProductIndexItem p, BudgetHint budget)
    {
        if (budget.Min is null && budget.Max is null) return true;
        var min = p.MinPrice ?? p.MaxPrice ?? p.RepresentativePrice;
        var max = p.MaxPrice ?? p.MinPrice ?? p.RepresentativePrice;
        if (min is null || max is null) return true;
        var minAllowed = budget.Min is null ? 0 : budget.Min.Value * (1m - BudgetToleranceRatio);
        var maxAllowed = budget.Max is null ? decimal.MaxValue : budget.Max.Value * (1m + BudgetToleranceRatio);
        return !(max < minAllowed || min > maxAllowed);
    }
    private static string NormalizeForSearch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);
        return Regex.Replace(noDiacritics.ToLowerInvariant(), @"\s+", " ").Trim();
    }
    private static HashSet<string> TokenizeForSearch(string? text)
    {
        return NormalizeForSearch(text)
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '/', '\\', '-', '_', '+' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .Where(t => !SearchStopWords.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    private static int ScoreProduct(ProductIndexItem p, HashSet<string> tokens, BudgetHint budget, SearchIntent intent)
    {
        var score = 0;
        var nameLower = NormalizeForSearch(p.Name);
        var brandLower = NormalizeForSearch(p.BrandName);
        var searchText = p.SearchText;
        var tokenHits = 0;
        foreach (var t in tokens)
        {
            if (t.Length < 2) continue;
            if (nameLower.Contains(t))
            {
                score += 12;
                tokenHits++;
            }
            if (!string.IsNullOrWhiteSpace(brandLower) && brandLower.Contains(t))
            {
                score += 9;
                tokenHits++;
            }
            if (searchText.Contains(t))
            {
                score += 4;
                tokenHits++;
            }
            if (p.CategorySlugs.Any(c => NormalizeForSearch(c).Contains(t)))
                score += 6;
        }
        if (tokenHits > 1)
            score += Math.Min(tokenHits * 2, 12);
        if (intent.LaptopPreferred)
            score += p.CategorySlugs.Any(c => c.Equals(LaptopCategorySlug, StringComparison.OrdinalIgnoreCase)) ? 14 : -8;
        if (intent.PcPreferred)
            score += p.CategorySlugs.Any(c => PcCategorySlugs.Contains(c, StringComparer.OrdinalIgnoreCase)) ? 8 : 0;
        if (intent.GamingPreferred)
            score += searchText.Contains("rtx") || searchText.Contains("fps") || searchText.Contains("gaming") ? 9 : 0;
        if (intent.OfficePreferred)
            score += searchText.Contains("office") || searchText.Contains("van phong") || searchText.Contains("tiet kiem dien") ? 6 : 0;
        if (budget.Min is not null || budget.Max is not null || budget.Target is not null)
        {
            var rep = p.RepresentativePrice ?? p.MinPrice ?? p.MaxPrice;
            if (rep is not null)
            {
                if (budget.Target is not null && budget.Target > 0)
                {
                    var diffRatio = Math.Abs(rep.Value - budget.Target.Value) / budget.Target.Value;
                    score += Math.Max(0, (int)Math.Round(14 - diffRatio * 26));
                }
                if (budget.Max is not null && rep > budget.Max * (1m + BudgetToleranceRatio))
                    score -= 10;
                if (budget.Min is not null && rep < budget.Min * (1m - BudgetToleranceRatio))
                    score -= 4;
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
        var tokens = TokenizeForSearch(message);
        var safeMessage = message ?? "";
        var budget = MergeBudgetHint(ExtractBudgetHintVnd(safeMessage), toolBudgetMaxVnd);
        var intent = DetectSearchIntent(safeMessage, datasetKey);
        var matched = fullDataset
            .Where(p => IsBudgetCompatible(p, budget))
            .Select(p => new { p, score = ScoreProduct(p, tokens, budget, intent) })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.p.Name)
            .Select(x => x.p)
            .ToList();
        // If dataset-specific filtering is too strict (e.g. slug mismatch), fallback to full catalog.
        if (matched.Count == 0 && !datasetKey.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var allDataset = new ProductsDataset("all", Array.Empty<string>());
            var fullAll = await GetOrBuildFullDatasetAsync(allDataset, cancellationToken);
            matched = fullAll
                .Where(p => IsBudgetCompatible(p, budget))
                .Select(p => new { p, score = ScoreProduct(p, tokens, budget, intent) })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.p.Name)
                .Select(x => x.p)
                .ToList();
            if (matched.Count > 0)
            {
                dataset = allDataset;
                datasetKey = allDataset.CacheKeySuffix;
                stateKey = $"{SearchStateKeyPrefix}:{sid}:{datasetKey}";
            }
        }
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
        var lower = NormalizeForSearch(text);
        if (lower.Contains("laptop") || lower.Contains("notebook") || lower.Contains("ultrabook") || lower.Contains("may tinh xach tay"))
            return new ProductsDataset("laptop", [LaptopCategorySlug]);
        if (lower.Contains("xay dung cau hinh") ||
            lower.Contains("cau hinh may tinh") ||
            lower.Contains("build pc") ||
            lower.Contains("pc"))
        {
            return new ProductsDataset("pc", PcCategorySlugs);
        }
        return new ProductsDataset("all", Array.Empty<string>());
    }
    //Ưu tiên model đang chọn, sau đó BotChat:FallbackModels (hoặc BuiltInFallbackModels)
    private IReadOnlyList<string> ResolveModelFallbackChain(string primaryNormalized)
    {
        var list = new List<string>();
        void AddIfAllowed(string? m)
        {
            var n = NormalizeModelName(m);
            if (string.IsNullOrWhiteSpace(n) || !IsAllowedModel(n)) return;
            if (list.Exists(x => x.Equals(n, StringComparison.OrdinalIgnoreCase))) return;
            list.Add(n);
        }
        AddIfAllowed(primaryNormalized);
        var fromConfig = _configuration.GetSection("BotChat:FallbackModels").Get<string[]>();
        if (fromConfig is { Length: > 0 })
        {
            foreach (var item in fromConfig)
                AddIfAllowed(item);
        }
        else
        {
            foreach (var item in BuiltInFallbackModels)
                AddIfAllowed(item);
        }
        return list;
    }
    //503 / 429 và một số mã Google's API — có thể thử model khác hoặc gọi lại sau
    private static bool IsTransientGeminiFailure(HttpStatusCode status, string raw)
    {
        if (status == HttpStatusCode.ServiceUnavailable) return true;
        if (status == HttpStatusCode.TooManyRequests) return true;
        if ((int)status == 429) return true;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("error", out var err)) return false;
            if (err.TryGetProperty("status", out var st))
            {
                var s = st.GetString();
                if (s is "UNAVAILABLE" or "RESOURCE_EXHAUSTED" or "DEADLINE_EXCEEDED") return true;
            }
            if (err.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number)
            {
                var n = c.GetInt32();
                if (n is 503 or 429) return true;
            }
        }
        catch (JsonException)
        {
            // ignore
        }
        return false;
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
