using Application.DTOs.Orders;

namespace Application.Interfaces.Services
{
    public interface IEmailService
    {
        Task SendOrderConfirmationAsync(OrderConfirmationEmailRequest request);
    }
}
