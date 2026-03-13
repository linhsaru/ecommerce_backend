using Application.Common;
using Application.DTOs.Carts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Services
{
    public interface ICartService
    {
        //userId = null: khách, trả về dữ liệu lưu LocalStorage; userId có giá trị: kiểm tra tồn kho và lưu DB.
        Task<Result<CartResponse>> AddToCartAsync(Guid? userId, AddToCartDto dto, CancellationToken cancellationToken = default);
        //userId = null: trả giỏ rỗng (frontend dùng LocalStorage); userId có giá trị: trả giỏ từ DB.
        Task<Result<CartResponse>> GetCartAsync(Guid? userId, CancellationToken cancellationToken = default);
    }
}
