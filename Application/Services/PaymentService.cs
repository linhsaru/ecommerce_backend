using Application.Common;
using Application.DTOs.Orders;
using Application.DTOs.Payments;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Application.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IAppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<PaymentService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public PaymentService(
        IAppDbContext dbContext,
        IConfiguration configuration,
        IUserRepository userRepository,
        IEmailService emailService,
        IHttpClientFactory httpClientFactory,
        ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _userRepository = userRepository;
        _emailService = emailService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Result<CreateVnPayPaymentResponse>> CreateVnPayPaymentUrlAsync(
        CreateVnPayPaymentRequest request,
        string clientIp,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order == null)
            return Result<CreateVnPayPaymentResponse>.Fail("NOT_FOUND", "Order not found.");

        if (order.PaymentStatus == PaymentStatus.paid)
            return Result<CreateVnPayPaymentResponse>.Fail("VALIDATION_ERROR", "Order has already been paid.");

        var tmnCode = _configuration["VnPay:TmnCode"]?.Trim();
        var hashSecret = _configuration["VnPay:HashSecret"]?.Trim();
        var baseUrl = _configuration["VnPay:BaseUrl"]?.Trim();
        var returnUrl = string.IsNullOrWhiteSpace(request.ReturnUrl)
            ? _configuration["VnPay:ReturnUrl"]?.Trim()
            : request.ReturnUrl.Trim();
        var version = _configuration["VnPay:Version"] ?? "2.1.0";
        var command = _configuration["VnPay:Command"] ?? "pay";
        var currCode = _configuration["VnPay:CurrCode"] ?? "VND";
        var locale = _configuration["VnPay:Locale"] ?? "vn";

        if (string.IsNullOrWhiteSpace(tmnCode)
            || string.IsNullOrWhiteSpace(hashSecret)
            || string.IsNullOrWhiteSpace(baseUrl)
            || string.IsNullOrWhiteSpace(returnUrl))
        {
            return Result<CreateVnPayPaymentResponse>.Fail("CONFIG_ERROR", "VNPAY configuration is missing.");
        }

        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                         RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                              ? "SE Asia Standard Time"
                              : "Asia/Ho_Chi_Minh");

        var createDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
        var expireDate = createDate.AddMinutes(15);
        var txnRef = $"{order.Id:N}-{createDate:HHmmss}";
        var amount = Convert.ToInt64(decimal.Round(order.TotalAmount * 100m, 0, MidpointRounding.AwayFromZero));
        var orderInfo = string.IsNullOrWhiteSpace(request.OrderDescription)
            ? $"Thanh toan don hang {order.OrderNo}"
            : request.OrderDescription.Trim();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Amount = order.TotalAmount,
            Method = PaymentMethod.vnpay,
            Status = PaymentStatus.unpaid,
            Provider = "VNPAY",
            ProviderTxnId = txnRef,
            RawPayload = JsonSerializer.Serialize(new
            {
                recipientEmail = request.RecipientEmail?.Trim()
            }),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = version,
            ["vnp_Command"] = command,
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = amount.ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = createDate.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = currCode,
            ["vnp_IpAddr"] = NormalizeIp(clientIp),
            ["vnp_Locale"] = locale,
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = txnRef,
            ["vnp_ExpireDate"] = expireDate.ToString("yyyyMMddHHmmss")
        };

        var hashData = BuildQuery(parameters, encodeValues: true);
        var secureHash = ComputeHmacSha512(hashSecret, hashData);
        var queryString = BuildQuery(parameters, encodeValues: true);
        var paymentUrl = $"{baseUrl}?{queryString}&vnp_SecureHashType=HmacSHA512&vnp_SecureHash={secureHash}";

        return Result<CreateVnPayPaymentResponse>.Ok(new CreateVnPayPaymentResponse
        {
            OrderId = order.Id,
            PaymentUrl = paymentUrl,
            TransactionRef = txnRef,
            ExpireAt = new DateTimeOffset(expireDate, TimeSpan.Zero)
        });
    }

    public async Task<Result<CreateVietQrPaymentResponse>> CreateVietQrPaymentAsync(
        CreateVietQrPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order == null)
            return Result<CreateVietQrPaymentResponse>.Fail("NOT_FOUND", "Order not found.");

        if (order.PaymentStatus == PaymentStatus.paid)
            return Result<CreateVietQrPaymentResponse>.Fail("VALIDATION_ERROR", "Order has already been paid.");

        var bankBin = _configuration["VietQr:BankBin"]?.Trim();
        var accountNumber = _configuration["VietQr:AccountNumber"]?.Trim();
        var accountName = _configuration["VietQr:AccountName"]?.Trim();
        var template = _configuration["VietQr:Template"]?.Trim();
        var expireMinutesRaw = _configuration["VietQr:ExpireMinutes"];

        if (string.IsNullOrWhiteSpace(bankBin) || string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(accountName))
            return Result<CreateVietQrPaymentResponse>.Fail("CONFIG_ERROR", "VietQR configuration is missing.");

        var expireMinutes = int.TryParse(expireMinutesRaw, out var parsedExpireMinutes) && parsedExpireMinutes > 0
            ? parsedExpireMinutes
            : 15;
        template = string.IsNullOrWhiteSpace(template) ? "compact2" : template;

        var transactionRef = $"VQR{order.Id:N}".ToUpperInvariant()[..18];
        var transferContent = $"SEVQR{order.OrderNo}";
        var expireAt = DateTimeOffset.UtcNow.AddMinutes(expireMinutes);

        var payment = await _dbContext.Payments
            .Where(p => p.OrderId == order.Id && p.Method == PaymentMethod.bank_transfer)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment == null)
        {
            payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = order.TotalAmount,
                Method = PaymentMethod.bank_transfer,
                Status = PaymentStatus.unpaid,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.Payments.Add(payment);
        }

        payment.Provider = "VIETQR";
        payment.ProviderTxnId = transactionRef;
        payment.RawPayload = JsonSerializer.Serialize(new
        {
            transferContent,
            recipientEmail = request.RecipientEmail?.Trim(),
            qrExpireAt = expireAt
        });
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var amount = decimal.Round(order.TotalAmount, 0, MidpointRounding.AwayFromZero);
        var qrCodeUrl =
            $"https://img.vietqr.io/image/{bankBin}-{accountNumber}-{template}.png" +
            $"?amount={amount:0}" +
            $"&addInfo={Uri.EscapeDataString(transferContent)}" +
            $"&accountName={Uri.EscapeDataString(accountName)}";

        return Result<CreateVietQrPaymentResponse>.Ok(new CreateVietQrPaymentResponse
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            TransactionRef = transactionRef,
            QrCodeUrl = qrCodeUrl,
            BankBin = bankBin,
            AccountNumber = accountNumber,
            AccountName = accountName,
            TransferContent = transferContent,
            Amount = amount,
            PaymentStatus = payment.Status,
            ExpireAt = expireAt
        });
    }

    public async Task<Result<VietQrPaymentStatusResponse>> CheckVietQrPaymentStatusAsync(
        Guid orderId,
        string transactionRef,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p =>
                p.OrderId == orderId &&
                p.Provider == "VIETQR" &&
                p.ProviderTxnId == transactionRef, cancellationToken);

        if (payment == null || payment.Order == null)
            return Result<VietQrPaymentStatusResponse>.Fail("NOT_FOUND", "VietQR payment not found.");

        if (payment.Status != PaymentStatus.paid)
        {
            var autoConfirmResult = await TryAutoConfirmVietQrViaSePayAsync(payment, cancellationToken);
            if (autoConfirmResult.IsFailure)
            {
                _logger.LogWarning(
                    "Skip SePay auto-confirm due to error. OrderId={OrderId}, TxnRef={TxnRef}, Error={Error}",
                    payment.OrderId,
                    payment.ProviderTxnId,
                    autoConfirmResult.Errors.FirstOrDefault()?.Message);
            }
        }

        return Result<VietQrPaymentStatusResponse>.Ok(new VietQrPaymentStatusResponse
        {
            OrderId = payment.OrderId,
            OrderNo = payment.Order.OrderNo,
            TransactionRef = payment.ProviderTxnId ?? string.Empty,
            Amount = payment.Amount,
            PaymentStatus = payment.Status,
            IsSuccess = payment.Status == PaymentStatus.paid
        });
    }

    private async Task<Result> TryAutoConfirmVietQrViaSePayAsync(Payment payment, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["SePay:ApiKey"]?.Trim();
        var baseUrl = _configuration["SePay:BaseUrl"]?.Trim();
        var transactionsPath = _configuration["SePay:TransactionsPath"]?.Trim() ?? "/userapi/transactions/list";
        var lookbackMinutesRaw = _configuration["SePay:LookbackMinutes"];
        var lookbackMinutes = int.TryParse(lookbackMinutesRaw, out var parsed) && parsed > 0 ? parsed : 60;

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
            return Result.Fail("CONFIG_ERROR", "SePay configuration is missing.");

        if (string.IsNullOrWhiteSpace(payment.ProviderTxnId))
            return Result.Fail("VALIDATION_ERROR", "Missing VietQR transaction reference.");

        var transferContent = GetTransferContentFromRawPayload(payment.RawPayload) ?? payment.ProviderTxnId!;
        var orderCode = payment.Order.OrderNo;
        var requestUrl =
            $"{baseUrl.TrimEnd('/')}{(transactionsPath.StartsWith('/') ? transactionsPath : $"/{transactionsPath}")}" +
            $"?transaction_content={Uri.EscapeDataString(orderCode)}" +
            $"&from_date={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-lookbackMinutes).ToString("yyyy-MM-dd"))}";

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "SePay request failed due to network/DNS issue.");
            return Result.Fail("SEPAY_UNAVAILABLE", "SePay service is currently unavailable.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "SePay request timed out.");
            return Result.Fail("SEPAY_TIMEOUT", "SePay request timed out.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SePay query failed. StatusCode={StatusCode}", response.StatusCode);
                return Result.Fail("SEPAY_ERROR", "Failed to query SePay transactions.");
            }

            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(rawContent))
                return Result.Fail("SEPAY_EMPTY_RESPONSE", "Empty response from SePay.");

            var trimmedContent = rawContent.TrimStart();
            if (trimmedContent.StartsWith("<", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "SePay returned HTML content. Check SePay BaseUrl/TransactionsPath. RequestUrl={RequestUrl}",
                    requestUrl);
                return Result.Fail("SEPAY_INVALID_RESPONSE", "SePay returned non-JSON content.");
            }

            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(rawContent);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON response from SePay. RequestUrl={RequestUrl}", requestUrl);
                return Result.Fail("SEPAY_INVALID_RESPONSE", "Invalid SePay response.");
            }

            using (json)
            {
                if (!TryFindMatchedSePayTransaction(json.RootElement, orderCode, payment.Amount))
                    return Result.Fail("PAYMENT_NOT_FOUND", "No matched paid transaction found on SePay.");
            }
        }

        var confirmResult = await ConfirmVietQrPaymentAsync(new VietQrWebhookRequest
        {
            TransactionRef = payment.ProviderTxnId!,
            Amount = payment.Amount,
            Description = transferContent
        }, cancellationToken);

        return confirmResult.IsSuccess
            ? Result.Ok()
            : Result.Fail(confirmResult.Errors.ToArray());
    }

    private static bool TryFindMatchedSePayTransaction(JsonElement root, string transferContent, decimal amount)
    {
        JsonElement transactions = default;
        if (root.ValueKind == JsonValueKind.Array)
        {
            transactions = root;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("data", out var dataElement))
            {
                if (dataElement.ValueKind == JsonValueKind.Array)
                    transactions = dataElement;
                else if (dataElement.ValueKind == JsonValueKind.Object
                         && dataElement.TryGetProperty("transactions", out var nestedTransactions))
                    transactions = nestedTransactions;
            }
            else if (root.TryGetProperty("transactions", out var transactionsElement))
            {
                transactions = transactionsElement;
            }
        }

        if (transactions.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var tx in transactions.EnumerateArray())
        {
            var txAmount = GetDecimal(tx, "amount_in")
                ?? GetDecimal(tx, "amount")
                ?? GetDecimal(tx, "transferAmount");
            if (!txAmount.HasValue || decimal.Round(txAmount.Value, 0, MidpointRounding.AwayFromZero) != decimal.Round(amount, 0, MidpointRounding.AwayFromZero))
                continue;

            var txContent =
                GetString(tx, "transaction_content") ??
                GetString(tx, "description") ??
                GetString(tx, "content");

            if (string.IsNullOrWhiteSpace(txContent) || !txContent.Contains(transferContent, StringComparison.OrdinalIgnoreCase))
                continue;

            var status = (GetString(tx, "status") ?? string.Empty).Trim().ToLowerInvariant();
            var isSuccessStatus = string.IsNullOrWhiteSpace(status)
                                  || status is "success" or "paid" or "completed";

            if (isSuccessStatus)
                return true;
        }

        return false;
    }

    public async Task<Result<VietQrPaymentStatusResponse>> ConfirmVietQrPaymentAsync(
        VietQrWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestedCode = GetRequestedTransferCode(request);
        var requestedTxnRef = string.IsNullOrWhiteSpace(request.TransactionRef) ? null : request.TransactionRef.Trim();
        var requestedAmount = request.Amount ?? request.TransferAmount;

        if (!requestedAmount.HasValue || requestedAmount.Value <= 0)
            return Result<VietQrPaymentStatusResponse>.Fail("VALIDATION_ERROR", "Invalid transfer amount.");

        Payment? payment = null;

        if (!string.IsNullOrWhiteSpace(requestedTxnRef))
        {
            payment = await _dbContext.Payments
                .Include(p => p.Order)
                .ThenInclude(o => o!.OrderItems)
                .FirstOrDefaultAsync(p =>
                    p.Provider == "VIETQR" &&
                    p.ProviderTxnId == requestedTxnRef, cancellationToken);
        }

        if (payment == null && !string.IsNullOrWhiteSpace(requestedCode))
        {
            payment = await _dbContext.Payments
                .Include(p => p.Order)
                .ThenInclude(o => o!.OrderItems)
                .Where(p => p.Provider == "VIETQR" && p.RawPayload != null)
                .FirstOrDefaultAsync(p => p.RawPayload!.Contains(requestedCode), cancellationToken);
        }

        if (payment == null || payment.Order == null)
            return Result<VietQrPaymentStatusResponse>.Fail("NOT_FOUND", "VietQR payment transaction not found.");

        if (payment.Status == PaymentStatus.paid)
        {
            return Result<VietQrPaymentStatusResponse>.Ok(new VietQrPaymentStatusResponse
            {
                OrderId = payment.OrderId,
                OrderNo = payment.Order.OrderNo,
                TransactionRef = payment.ProviderTxnId ?? requestedTxnRef ?? string.Empty,
                Amount = payment.Amount,
                PaymentStatus = payment.Status,
                IsSuccess = true
            });
        }

        if (decimal.Round(requestedAmount.Value, 0, MidpointRounding.AwayFromZero) != decimal.Round(payment.Amount, 0, MidpointRounding.AwayFromZero))
            return Result<VietQrPaymentStatusResponse>.Fail("VALIDATION_ERROR", "Amount mismatch.");

        var rawDescription = request.Description?.Trim() ?? request.Content?.Trim();
        var expectedTransferContent = GetTransferContentFromRawPayload(payment.RawPayload);
        if (!string.IsNullOrWhiteSpace(rawDescription) &&
            !rawDescription.Contains(payment.Order.OrderNo, StringComparison.OrdinalIgnoreCase) &&
            !rawDescription.Contains(payment.ProviderTxnId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(expectedTransferContent) &&
            !rawDescription.Contains(expectedTransferContent, StringComparison.OrdinalIgnoreCase))
        {
            return Result<VietQrPaymentStatusResponse>.Fail("VALIDATION_ERROR", "Transaction description does not match.");
        }

        var recipientEmail = GetRecipientEmailFromRawPayload(payment.RawPayload);

        payment.Status = PaymentStatus.paid;
        payment.PaidAt = DateTimeOffset.UtcNow;
        payment.RawPayload = JsonSerializer.Serialize(new
        {
            recipientEmail,
            webhook = request
        });
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        payment.Order.PaymentStatus = PaymentStatus.paid;
        if (payment.Order.Status == OrderStatus.pending)
            payment.Order.Status = OrderStatus.confirmed;
        payment.Order.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await TrySendOrderConfirmationEmailAsync(payment.Order, recipientEmail, cancellationToken);

        return Result<VietQrPaymentStatusResponse>.Ok(new VietQrPaymentStatusResponse
        {
            OrderId = payment.OrderId,
            OrderNo = payment.Order.OrderNo,
            TransactionRef = payment.ProviderTxnId ?? requestedTxnRef ?? string.Empty,
            Amount = payment.Amount,
            PaymentStatus = payment.Status,
            IsSuccess = true
        });
    }

    public async Task<Result<VnPayCallbackResponse>> HandleVnPayCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default)
    {
        if (!queryParams.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrWhiteSpace(secureHash))
            return Result<VnPayCallbackResponse>.Fail("VALIDATION_ERROR", "Missing secure hash.");

        var hashSecret = _configuration["VnPay:HashSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(hashSecret))
            return Result<VnPayCallbackResponse>.Fail("CONFIG_ERROR", "VNPAY hash secret is missing.");

        var dataToVerify = queryParams
            .Where(kvp => kvp.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                          && kvp.Key != "vnp_SecureHash"
                          && kvp.Key != "vnp_SecureHashType")
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(k => k.Key, v => v.Value);

        var signData = BuildQuery(new SortedDictionary<string, string>(dataToVerify, StringComparer.Ordinal), encodeValues: true);
        var expectedHash = ComputeHmacSha512(hashSecret, signData);
        if (!string.Equals(expectedHash, secureHash, StringComparison.OrdinalIgnoreCase))
            return Result<VnPayCallbackResponse>.Fail("VALIDATION_ERROR", "Invalid signature from VNPAY.");

        if (!queryParams.TryGetValue("vnp_TxnRef", out var txnRef) || string.IsNullOrWhiteSpace(txnRef))
            return Result<VnPayCallbackResponse>.Fail("VALIDATION_ERROR", "Missing transaction reference.");

        var payment = await _dbContext.Payments
            .Include(p => p.Order)
            .ThenInclude(o => o!.OrderItems)
            .FirstOrDefaultAsync(p => p.ProviderTxnId == txnRef && p.Provider == "VNPAY", cancellationToken);

        if (payment == null)
            return Result<VnPayCallbackResponse>.Fail("NOT_FOUND", "Payment transaction not found.");

        var responseCode = queryParams.TryGetValue("vnp_ResponseCode", out var code) ? code : string.Empty;
        var isSuccess = responseCode == "00";

        var wasPaidBefore = payment.Status == PaymentStatus.paid;

        var recipientEmail = GetRecipientEmailFromRawPayload(payment.RawPayload);

        payment.Status = isSuccess ? PaymentStatus.paid : PaymentStatus.failed;
        payment.PaidAt = isSuccess ? DateTimeOffset.UtcNow : null;
        payment.RawPayload = JsonSerializer.Serialize(new
        {
            recipientEmail,
            callback = queryParams
        });
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        payment.Order.PaymentStatus = payment.Status;
        if (isSuccess && payment.Order.Status == OrderStatus.pending)
            payment.Order.Status = OrderStatus.confirmed;
        payment.Order.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isSuccess && !wasPaidBefore)
        {
            await TrySendOrderConfirmationEmailAsync(payment.Order, recipientEmail, cancellationToken);
        }

        var amount = queryParams.TryGetValue("vnp_Amount", out var amountRaw)
                     && long.TryParse(amountRaw, out var amountLong)
            ? amountLong / 100m
            : payment.Amount;

        return Result<VnPayCallbackResponse>.Ok(new VnPayCallbackResponse
        {
            OrderId = payment.OrderId,
            OrderNo = payment.Order?.OrderNo ?? string.Empty,
            TransactionRef = txnRef,
            ResponseCode = responseCode,
            Amount = amount,
            PaymentStatus = payment.Status,
            IsSuccess = isSuccess
        });
    }

    private async Task TrySendOrderConfirmationEmailAsync(Order order, string? fallbackEmail, CancellationToken cancellationToken)
    {
        try
        {
            if (order.OrderItems.Count == 0)
            {
                _logger.LogWarning("Skip VNPay confirmation email because order has no items. OrderId={OrderId}", order.Id);
                return;
            }

            string? targetEmail = null;
            string? recipientName = order.ShipRecipient;

            if (order.UserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(order.UserId.Value, cancellationToken);
                if (user != null)
                {
                    targetEmail = user.Email?.Trim();
                    recipientName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : order.ShipRecipient;
                }
            }

            if (string.IsNullOrWhiteSpace(targetEmail))
            {
                targetEmail = fallbackEmail?.Trim();
            }

            if (string.IsNullOrWhiteSpace(targetEmail))
            {
                _logger.LogWarning("Skip VNPay confirmation email because recipient email is empty. OrderId={OrderId}", order.Id);
                return;
            }

            await _emailService.SendOrderConfirmationAsync(new OrderConfirmationEmailRequest
            {
                RecipientEmail = targetEmail,
                RecipientName = recipientName,
                ReciptientAddress = order.ShipLine1,
                PhoneNumber = order.ShipPhone,
                OrderNo = order.OrderNo,
                OrderedAt = order.CreatedAt,
                PaymentStatus = order.PaymentStatus,
                TotalAmount = order.TotalAmount,
                Items = order.OrderItems.Select(item => new OrderConfirmationEmailItem
                {
                    ProductName = item.Name,
                    VariantName = item.VariantName,
                    Quantity = item.Quantity,
                    LineTotal = item.LineTotal
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send VNPay confirmation email for order {OrderId}", order.Id);
        }
    }

    private static string? GetRecipientEmailFromRawPayload(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return null;

        try
        {
            var node = JsonNode.Parse(rawPayload)?.AsObject();
            var recipientEmail = node?["recipientEmail"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(recipientEmail) ? null : recipientEmail.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetTransferContentFromRawPayload(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return null;

        try
        {
            var node = JsonNode.Parse(rawPayload)?.AsObject();
            var transferContent = node?["transferContent"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(transferContent) ? null : transferContent.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetRequestedTransferCode(VietQrWebhookRequest request)
    {
        var code = request.Code?.Trim();
        if (!string.IsNullOrWhiteSpace(code))
            return code;

        var source = request.Content?.Trim() ?? request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(source))
            return null;

        var match = Regex.Match(source, @"ORD\d{3,30}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var decimalValue))
            return decimalValue;

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static string ComputeHmacSha512(string key, string inputData)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string BuildQuery(SortedDictionary<string, string> parameters, bool encodeValues)
    {
        var sb = new StringBuilder();
        foreach (var pair in parameters.Where(p => !string.IsNullOrEmpty(p.Value)))
        {
            if (sb.Length > 0)
                sb.Append('&');
            sb.Append(pair.Key);
            sb.Append('=');
            sb.Append(encodeValues ? WebUtility.UrlEncode(pair.Value) : pair.Value);
        }
        return sb.ToString();
    }

    private static string NormalizeIp(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return "127.0.0.1";
        if (ipAddress == "::1")
            return "127.0.0.1";
        return ipAddress;
    }
}
