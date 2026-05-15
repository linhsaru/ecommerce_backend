using API.Common;
using API.Contracts;
using Application.DTOs.Brands;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{

    [ApiController]
    [Route("brands")]
    public class BrandController : BaseApiController
    {
        private readonly IBrandService _brandService;

        public BrandController(IBrandService brandService)
        {
            _brandService = brandService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<BrandDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
        {
            var result = await _brandService.GetAllAsync(cancellationToken);
            return Ok(ApiResponse<IEnumerable<BrandDto>>.Ok(result, traceId: HttpContext.TraceIdentifier));
        }

        [HttpGet("{slug}")]
        [ProducesResponseType(typeof(ApiResponse<BrandDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken = default)
        {
            var result = await _brandService.GetBySlugAsync(slug, cancellationToken);
            if (result == null)
                return NotFound(ApiResponse<BrandDto>.Fail("Not found", null, HttpContext.TraceIdentifier));
            return Ok(ApiResponse<BrandDto>.Ok(result, traceId: HttpContext.TraceIdentifier));

        }
    }
}
