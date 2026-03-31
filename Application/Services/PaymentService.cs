using Application.Common;
using Application.DTOs.Payments;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IAppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public PaymentService(IAppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
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
        var returnUrl = _configuration["VnPay:ReturnUrl"]?.Trim();
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
            RawPayload = null,
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
            .FirstOrDefaultAsync(p => p.ProviderTxnId == txnRef && p.Provider == "VNPAY", cancellationToken);

        if (payment == null)
            return Result<VnPayCallbackResponse>.Fail("NOT_FOUND", "Payment transaction not found.");

        var responseCode = queryParams.TryGetValue("vnp_ResponseCode", out var code) ? code : string.Empty;
        var isSuccess = responseCode == "00";

        payment.Status = isSuccess ? PaymentStatus.paid : PaymentStatus.failed;
        payment.PaidAt = isSuccess ? DateTimeOffset.UtcNow : null;
        payment.RawPayload = JsonSerializer.Serialize(queryParams);
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        payment.Order.PaymentStatus = payment.Status;
        if (isSuccess && payment.Order.Status == OrderStatus.pending)
            payment.Order.Status = OrderStatus.confirmed;
        payment.Order.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var amount = queryParams.TryGetValue("vnp_Amount", out var amountRaw)
                     && long.TryParse(amountRaw, out var amountLong)
            ? amountLong / 100m
            : payment.Amount;

        return Result<VnPayCallbackResponse>.Ok(new VnPayCallbackResponse
        {
            OrderId = payment.OrderId,
            TransactionRef = txnRef,
            ResponseCode = responseCode,
            Amount = amount,
            PaymentStatus = payment.Status,
            IsSuccess = isSuccess
        });
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
