using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialAnalytics.Api.Services;

namespace SocialAnalytics.Api.Controllers;

[ApiController]
public class I18nController : ControllerBase
{
    private readonly CentralLocalizationService _localization;

    public I18nController(CentralLocalizationService localization)
    {
        _localization = localization;
    }

    [HttpGet("api/i18n/manifest")]
    [AllowAnonymous]
    public async Task<ActionResult<LocalizationManifestResponse>> Manifest(CancellationToken ct = default) =>
        Ok(await _localization.GetManifestAsync(ct));

    [HttpGet("api/i18n/bundle/{locale?}")]
    [AllowAnonymous]
    public async Task<ActionResult<LocalizationBundleResponse>> Bundle(string? locale = null, CancellationToken ct = default) =>
        Ok(await _localization.GetBundleAsync(locale, ct));

    [HttpGet("api/settings/localization")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SiteLocalizationAdminStateResponse>> GetLocalizationSettings(CancellationToken ct = default) =>
        Ok(await _localization.GetAdminStateAsync(ct));

    [HttpPut("api/settings/localization")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SiteLocalizationAdminStateResponse>> UpdateLocalizationSettings(
        [FromBody] UpdateSiteLocalizationRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? User.Identity?.Name
                     ?? "admin";
        return Ok(await _localization.UpdateLocalSettingsAsync(request, userId, ct));
    }
}
