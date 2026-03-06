using Microsoft.AspNetCore.Mvc;

namespace API.Common;

/// <summary>
/// Base controller: cac controller ke thua va dat [Route("api/...")] rieng.
/// </summary>
[ApiController]
public abstract class BaseApiController : ControllerBase
{
}
