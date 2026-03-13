using Application.DTOs.Brands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Services
{
    public interface IBrandService
    {
            Task<IEnumerable<BrandDto>> GetAllAsync();
            Task<BrandDto?> GetBySlugAsync(string slug);
    }
}
