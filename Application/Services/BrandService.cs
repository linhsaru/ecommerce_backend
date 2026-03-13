using Application.DTOs.Brands;
using Application.Interfaces.Services;
using Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class BrandService : IBrandService
    {
        private readonly IBrandRepository _brandRpository;

        public BrandService(IBrandRepository brandRpository)
        {
            _brandRpository = brandRpository;
        }

        public async Task<IEnumerable<BrandDto>> GetAllAsync()
        {
            var brands = await _brandRpository.GetAllAsync();
            return brands.Select(b => new BrandDto
            {
                Id = b.Id,
                Name = b.Name,
                Slug = b.Slug,
            });
        }

        public Task<BrandDto?> GetBySlugAsync(string slug)
        {
            return _brandRpository.GetBySlugAsync(slug)
                .ContinueWith(task =>
                {
                    var brand = task.Result;
                    if (brand == null) return null;
                    return new BrandDto
                    {
                        Id = brand.Id,
                        Name = brand.Name,
                        Slug = brand.Slug,
                    };
                });
        }
    }
}
