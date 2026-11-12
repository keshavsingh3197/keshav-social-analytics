using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Models;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EarningsController : ControllerBase
{
    private readonly RevenueCalculatorService _calculatorService;

    public EarningsController(RevenueCalculatorService calculatorService)
    {
        _calculatorService = calculatorService;
    }

    [HttpPost("calculate")]
    public ActionResult<EarningsReport> Calculate([FromBody] EarningsCalculationRequest request)
    {
        var result = _calculatorService.CalculateEarnings(request);
        return Ok(result);
    }

    [HttpGet("default")]
    public ActionResult<EarningsReport> GetDefaultProjection()
    {
        var defaultReq = new EarningsCalculationRequest();
        var result = _calculatorService.CalculateEarnings(defaultReq);
        return Ok(result);
    }
}
