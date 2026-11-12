using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EarningsController : ControllerBase
{
    private readonly RevenueCalculatorService _calculatorService;
    private readonly SocialConfigService _configService;

    public EarningsController(RevenueCalculatorService calculatorService, SocialConfigService configService)
    {
        _calculatorService = calculatorService;
        _configService = configService;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<EarningsReport>> Calculate([FromBody] EarningsCalculationRequest request)
    {
        var result = await _calculatorService.CalculateEarningsAsync(request);
        return Ok(result);
    }

    [HttpGet("default")]
    public async Task<ActionResult<EarningsReport>> GetDefaultProjection()
    {
        var result = await _calculatorService.GetDefaultProjectionAsync();
        return Ok(result);
    }

    [HttpGet("config")]
    public async Task<ActionResult<EarningsConfig>> GetConfig()
    {
        var config = await _configService.GetEarningsConfigAsync();
        return Ok(config);
    }
}
