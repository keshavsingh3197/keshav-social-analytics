using Microsoft.AspNetCore.Mvc;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "SocialAnalytics.Api",
            version = "1.0.0",
            time = DateTime.UtcNow
        });
    }
}
