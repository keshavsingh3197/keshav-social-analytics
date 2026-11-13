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

    public async Task<List<FunnelCampaign>> GetCampaignsForUserAsync(
        string? callerUserId,
        bool isAdmin,
        bool activeOnly = true)
    {
        if (string.IsNullOrWhiteSpace(callerUserId))
            return [];

        var list = await _store.GetListAsync(Collection, SocialSeedData.DefaultCampaigns);
        return list
            .Where(c => (!activeOnly || c.IsActive) && IsOwnedByCaller(c, callerUserId, isAdmin))
            .OrderByDescending(c => c.CreatedAt)
            .ToList();
    }

    public static bool IsOwnedByCaller(FunnelCampaign c, string? callerUserId, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(callerUserId))
            return false;

        if (isAdmin)
            return string.Equals(c.OwnerUserId, callerUserId, StringComparison.Ordinal) ||
                   c.IsAdminOwned ||
                   string.IsNullOrWhiteSpace(c.OwnerUserId);

        return !c.IsAdminOwned && string.Equals(c.OwnerUserId, callerUserId, StringComparison.Ordinal);
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

    public async Task<FunnelCampaign?> UpsertUserCampaignAsync(
        string callerUserId,
        bool isAdmin,
        FunnelCampaign campaign)
    {
        if (string.IsNullOrWhiteSpace(callerUserId))
            return null;

        var list = await _store.GetListAsync(Collection, SocialSeedData.DefaultCampaigns);
        if (!string.IsNullOrWhiteSpace(campaign.Id))
        {
            var existing = list.FirstOrDefault(c => c.Id.Equals(campaign.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null && !IsOwnedByCaller(existing, callerUserId, isAdmin))
                return null;
        }
        else
        {
            campaign.Id = $"f-{Guid.NewGuid():N}"[..10];
        }

        campaign.OwnerUserId = callerUserId;
        campaign.IsAdminOwned = isAdmin;

        var desiredSlug = Slugify(string.IsNullOrWhiteSpace(campaign.TrackingSlug) ? campaign.Title : campaign.TrackingSlug);
        var slugTakenByOther = list.Any(c =>
            !c.Id.Equals(campaign.Id, StringComparison.OrdinalIgnoreCase) &&
            c.TrackingSlug.Equals(desiredSlug, StringComparison.OrdinalIgnoreCase));
        if (slugTakenByOther)
            desiredSlug = $"{desiredSlug}-{Guid.NewGuid():N}"[..Math.Min(desiredSlug.Length + 5, 36)];

        campaign.TrackingSlug = desiredSlug;
        return await UpsertCampaignAsync(campaign);
    }

    public async Task<bool> DeleteUserCampaignAsync(string callerUserId, bool isAdmin, string id)
    {
        var list = await _store.GetListAsync(Collection, SocialSeedData.DefaultCampaigns);
        var target = list.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (target is null || !IsOwnedByCaller(target, callerUserId, isAdmin))
            return false;
        return await DeleteCampaignAsync(target.Id);
    }

    public async Task<FunnelCampaign?> RecordUserConversionAsync(string callerUserId, bool isAdmin, string idOrSlug)
    {
        var list = await _store.GetListAsync(Collection, SocialSeedData.DefaultCampaigns);
        var target = list.FirstOrDefault(c =>
            (c.Id.Equals(idOrSlug, StringComparison.OrdinalIgnoreCase) ||
             c.TrackingSlug.Equals(idOrSlug, StringComparison.OrdinalIgnoreCase)) &&
            IsOwnedByCaller(c, callerUserId, isAdmin));
        if (target is null) return null;
        return await RecordConversionAsync(target.Id);
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
