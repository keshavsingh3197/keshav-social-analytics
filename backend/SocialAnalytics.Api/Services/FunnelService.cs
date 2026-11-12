using System.Text.RegularExpressions;
using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

public sealed partial class FunnelService
{
    private const string Collection = "funnel_campaigns";
    private readonly HybridDataStore _store;

    public FunnelService(HybridDataStore store)
    {
        _store = store;
    }

    public async Task<List<FunnelCampaign>> GetCampaignsAsync(bool activeOnly = true)
    {
        var list = await _store.GetListAsync(Collection, SocialSeedData.DefaultCampaigns);
        return (activeOnly ? list.Where(c => c.IsActive) : list)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();
    }

    public async Task<FunnelCampaign> UpsertCampaignAsync(FunnelCampaign campaign)
    {
        if (string.IsNullOrWhiteSpace(campaign.Id))
            campaign.Id = $"f-{Guid.NewGuid():N}"[..10];

        if (string.IsNullOrWhiteSpace(campaign.TrackingSlug))
            campaign.TrackingSlug = Slugify(campaign.Title);

        campaign.TrackingSlug = Slugify(campaign.TrackingSlug);
        campaign.ConversionRatePercentage = campaign.TotalClicks > 0
            ? Math.Round((double)campaign.Conversions / campaign.TotalClicks * 100, 1)
            : 0;

        if (campaign.CreatedAt == default)
            campaign.CreatedAt = DateTime.UtcNow;

        return await _store.UpsertItemAsync(Collection, campaign.Id, c => c.Id, campaign, SocialSeedData.DefaultCampaigns);
    }

    public Task<bool> DeleteCampaignAsync(string id) =>
        _store.DeleteItemAsync(Collection, id, c => c.Id, SocialSeedData.DefaultCampaigns);

    public async Task<FunnelCampaign?> TrackClickAsync(string slug)
    {
        var list = await _store.GetListAsync(Collection, SocialSeedData.DefaultCampaigns);
        var campaign = list.FirstOrDefault(c =>
            c.IsActive && c.TrackingSlug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        if (campaign is null) return null;

        campaign.TotalClicks++;
        campaign.ConversionRatePercentage = campaign.TotalClicks > 0
            ? Math.Round((double)campaign.Conversions / campaign.TotalClicks * 100, 1)
            : 0;

        await _store.UpsertItemAsync(Collection, campaign.Id, c => c.Id, campaign, SocialSeedData.DefaultCampaigns);
        return campaign;
    }

    public async Task<FunnelCampaign?> RecordConversionAsync(string idOrSlug)
    {
        var list = await _store.GetListAsync(Collection, SocialSeedData.DefaultCampaigns);
        var campaign = list.FirstOrDefault(c =>
            c.Id.Equals(idOrSlug, StringComparison.OrdinalIgnoreCase) ||
            c.TrackingSlug.Equals(idOrSlug, StringComparison.OrdinalIgnoreCase));
        if (campaign is null) return null;

        campaign.Conversions++;
        if (campaign.TotalClicks < campaign.Conversions)
            campaign.TotalClicks = campaign.Conversions;

        campaign.ConversionRatePercentage = campaign.TotalClicks > 0
            ? Math.Round((double)campaign.Conversions / campaign.TotalClicks * 100, 1)
            : 0;

        await _store.UpsertItemAsync(Collection, campaign.Id, c => c.Id, campaign, SocialSeedData.DefaultCampaigns);
        return campaign;
    }

    private static string Slugify(string raw)
    {
        var cleaned = SlugRegex().Replace(raw.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? $"link-{Guid.NewGuid():N}"[..10] : cleaned;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
