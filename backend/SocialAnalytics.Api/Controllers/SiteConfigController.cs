using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SiteConfigController : ControllerBase
{
    private readonly SocialConfigService _configService;

    public SiteConfigController(SocialConfigService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<SiteConfig>> Get()
    {
        var config = await _configService.GetSiteConfigAsync();
        return Ok(config);
    }
}
