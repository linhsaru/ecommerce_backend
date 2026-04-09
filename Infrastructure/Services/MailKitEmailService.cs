using Application.DTOs.Orders;
using Application.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Globalization;
using System.Text;

namespace Infrastructure.Services
{
    public sealed class MailKitEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public MailKitEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOrderConfirmationAsync(OrderConfirmationEmailRequest request)
        {
            var host = _configuration["Email:SmtpHost"];
            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];
            var fromEmail = _configuration["Email:FromEmail"] ?? username;
            var fromName = _configuration["Email:FromName"] ?? "Ecommerce Shop";
            var portValue = _configuration["Email:Port"];
            var useSslValue = _configuration["Email:UseSsl"];

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("Email SMTP settings are missing.");
            }

            var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 587;
            var useSsl = bool.TryParse(useSslValue, out var parsedUseSsl) && parsedUseSsl;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(request.RecipientEmail));
            message.Subject = $"Xác nhận đơn hàng {request.OrderNo}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = BuildOrderConfirmationBody(request)
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            var secureSocket = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await smtp.ConnectAsync(host, port, secureSocket);
            await smtp.AuthenticateAsync(username, password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        private static string BuildOrderConfirmationBody(OrderConfirmationEmailRequest request)
        {
            var culture = new CultureInfo("vi-VN");
            var displayName = string.IsNullOrWhiteSpace(request.RecipientName) ? "bạn" : request.RecipientName;
            var vietnamTime = request.OrderedAt.ToOffset(TimeSpan.FromHours(7));

            var itemsHtml = string.Join("", request.Items.Select(item =>
            {
                var variant = string.IsNullOrWhiteSpace(item.VariantName) ? "" : $" ({item.VariantName})";
                return $@"
                <tr>
                    <td style='padding:8px;border:1px solid #ddd;'>{item.ProductName}{variant}</td>
                    <td style='padding:8px;border:1px solid #ddd;text-align:center;'>{item.Quantity}</td>
                </tr>";
                }));

            return $@"
            <div style='font-family: Arial, sans-serif; line-height:1.6; color:#333'>
                <h2 style='color:#2c7be5;'>Đặt hàng thành công</h2>

                <p>Xin chào <b>{displayName}</b>,</p>

                <p>Cảm ơn bạn đã đặt hàng tại <b>LH Computer</b>. Dưới đây là thông tin đơn hàng của bạn:</p>

                <table style='border-collapse: collapse; width: 100%; margin: 16px 0;'>
                    <tr>
                        <td style='padding:8px;'><b>Mã đơn hàng:</b></td>
                        <td style='padding:8px; color:#2c7be5;'><b>{request.OrderNo}</b></td>
                    </tr>
                    <tr>
                        <td style='padding:8px;'><b>Thời gian đặt:</b></td>
                        <td style='padding:8px;'>{vietnamTime:dd/MM/yyyy HH:mm:ss}</td>
                    </tr>
                    <tr>
                        <td style='padding:8px;'><b>Trạng thái thanh toán:</b></td>
                        <td style='padding:8px;'><b>{request.PaymentStatus}</b></td>
                    </tr>
                    <tr>
                        <td style='padding:8px;'><b>Tổng tiền:</b></td>
                        <td style='padding:8px; color:#e5533d; font-size:16px;'><b>{request.TotalAmount.ToString("N0", culture)} VND</b></td>
                    </tr>
                </table>

                <h3>Danh sách sản phẩm</h3>

                <table style='border-collapse: collapse; width: 100%;'>
                    <tr style='background-color:#f5f5f5;'>
                        <th style='padding:8px;border:1px solid #ddd;'>Sản phẩm</th>
                        <th style='padding:8px;border:1px solid #ddd;'>Số lượng</th>
                    </tr>
                    {itemsHtml}
                </table>

                <p style='margin-top:20px;'>
                    Nếu bạn có bất kỳ câu hỏi nào, vui lòng liên hệ với chúng tôi.
                </p>

                <p>Trân trọng,<br><b>LH Computer</b></p>
            </div>";
        }
    }
}
