using Microsoft.AspNetCore.Mvc;
namespace RoleFit.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        service = "RoleFit AI API",
        status = "Healthy",
        version = "0.2.0-mvp",
        timestampUtc = DateTimeOffset.UtcNow
    });
}
