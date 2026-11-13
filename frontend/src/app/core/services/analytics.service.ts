import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminStatus,
  ChannelAuditResult,
  ChannelMetrics,
  ChannelSearchCandidate,
  EarningsCalculationRequest,
  EarningsConfig,
  EarningsReport,
  EcosystemSyncSummary,
  FunnelCampaign,
  GrowthConfig,
  GrowthSuggestion,
  IntegrationSettingsView,
  PlatformSummary,
  PublicChannelAnalysisResult,
  PublicChannelLookupRequest,
  RecentFeedVideo,
  SiteConfig,
  SocialPlatform,
  UpdateIntegrationSettingsRequest,
  YouTubeOAuthCompleteResponse,
  YouTubeOAuthConfigPublicView,
  YouTubeOAuthStatusResponse
} from '../models/analytics.models';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  readonly backendOffline = signal(false);
  readonly activeLookup = signal<PublicChannelAnalysisResult | null>(null);
  readonly siteConfig = signal<SiteConfig | null>(null);

  getChannels(): Observable<ChannelMetrics[]> {
    return this.http.get<ChannelMetrics[]>(`${this.api}/analytics/channels`).pipe(
      tap(() => this.backendOffline.set(false)),
      catchError(() => {
        this.backendOffline.set(true);
        return of([]);
      })
    );
  }

  getOverview(): Observable<PlatformSummary[]> {
    return this.http.get<PlatformSummary[]>(`${this.api}/analytics/overview`).pipe(
      tap(() => this.backendOffline.set(false)),
      catchError(() => {
        this.backendOffline.set(true);
        return of([]);
      })
    );
  }

  getRecentVideos(limit = 16): Observable<RecentFeedVideo[]> {
    return this.http.get<RecentFeedVideo[]>(`${this.api}/analytics/videos?limit=${limit}`).pipe(
      catchError(() => of([]))
    );
  }

  getEarningsConfig(): Observable<EarningsConfig> {
    return this.http.get<EarningsConfig>(`${this.api}/earnings/config`).pipe(
      catchError(() => of({ platformBenchmarks: [], niches: [], opportunities: [] }))
    );
  }

  calculateEarnings(req: EarningsCalculationRequest): Observable<EarningsReport> {
    return this.http.post<EarningsReport>(`${this.api}/earnings/calculate`, req).pipe(
      catchError(() => of(this.calculateLocalEarnings(req)))
    );
  }

  auditChannel(handle: string, platform: SocialPlatform = 'YouTube'): Observable<ChannelAuditResult> {
    return this.http
      .get<ChannelAuditResult>(`${this.api}/recommendations/audit?handle=${encodeURIComponent(handle)}&platform=${platform}`)
      .pipe(catchError(() => of({ channelHandle: handle, healthScoreOutOf100: 0, prioritySuggestions: [], bestUploadTimes: [], highDemandKeywords: [] })));
  }

  getGrowthConfig(): Observable<GrowthConfig> {
    return this.http.get<GrowthConfig>(`${this.api}/recommendations/config`);
  }

  getSiteConfig(): Observable<SiteConfig> {
    return this.http.get<SiteConfig>(`${this.api}/siteconfig`).pipe(
      tap((cfg) => this.siteConfig.set(cfg)),
      catchError(() => {
        const fallback: SiteConfig = {
          brandName: 'SocialPulse',
          brandSubtitle: 'Creator Analytics',
          heroTitle: 'Social Channel Performance & Growth Hub',
          heroSubtitle: 'Search any YouTube or creator handle for live channel analytics, RPM forecasts, and growth intelligence.',
          youtubeSnippetHeader: 'Explore Connected Apps & Resources:',
          youtubeSnippetFooter: 'Subscribe for regular updates & new releases!',
          hashtags: '',
          defaultDestinationBaseUrl: '',
          adminPortalUrl: 'https://admin.keshavsingh.in'
        };
        this.siteConfig.set(fallback);
        return of(fallback);
      })
    );
  }

  searchChannels(query: string, platform: SocialPlatform = 'YouTube', limit = 6): Observable<ChannelSearchCandidate[]> {
    const q = query.trim();
    if (q.length < 2) return of([]);
    return this.http
      .get<ChannelSearchCandidate[]>(
        `${this.api}/analytics/search?q=${encodeURIComponent(q)}&platform=${encodeURIComponent(platform)}&limit=${limit}`
      )
      .pipe(catchError(() => of([])));
  }

  lookupChannel(req: PublicChannelLookupRequest): Observable<PublicChannelAnalysisResult> {
    return this.http.post<PublicChannelAnalysisResult>(`${this.api}/analytics/lookup`, req).pipe(
      tap((res) => this.activeLookup.set(res))
    );
  }

  autoAddMyChannel(req: PublicChannelLookupRequest): Observable<ChannelMetrics> {
    return this.http.post<ChannelMetrics>(`${this.api}/analytics/my-channels/auto-add`, req);
  }

  saveMyChannel(channel: ChannelMetrics): Observable<ChannelMetrics> {
    return this.http.post<ChannelMetrics>(`${this.api}/analytics/my-channels`, channel);
  }

  refreshMyChannel(id: string): Observable<ChannelMetrics> {
    return this.http.post<ChannelMetrics>(`${this.api}/analytics/my-channels/${encodeURIComponent(id)}/refresh`, {});
  }

  deleteMyChannel(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/analytics/my-channels/${encodeURIComponent(id)}`);
  }

  getYouTubeOAuthPublicConfig(): Observable<YouTubeOAuthConfigPublicView> {
    return this.http
      .get<YouTubeOAuthConfigPublicView>(`${this.api}/integrations/youtube/config`)
      .pipe(catchError(() => of({ isOAuthConfigured: false, isApiKeyConfigured: false, redirectUriHint: '' })));
  }

  getYouTubeOAuthStatus(): Observable<YouTubeOAuthStatusResponse> {
    return this.http
      .get<YouTubeOAuthStatusResponse>(`${this.api}/integrations/youtube/status`)
      .pipe(catchError(() => of({ isOAuthConfigured: false, connections: [] })));
  }

  startYouTubeOAuth(returnPath = '/dashboard'): Observable<{ url: string }> {
    return this.http.post<{ url: string }>(`${this.api}/integrations/youtube/oauth/start`, { returnPath });
  }

  completeYouTubeOAuth(code: string, state: string): Observable<YouTubeOAuthCompleteResponse> {
    return this.http.post<YouTubeOAuthCompleteResponse>(`${this.api}/integrations/youtube/oauth/complete`, { code, state });
  }

  syncYouTubeConnection(channelId: string): Observable<ChannelMetrics> {
    return this.http.post<ChannelMetrics>(`${this.api}/integrations/youtube/connections/${encodeURIComponent(channelId)}/sync`, {});
  }

  disconnectYouTubeConnection(channelId: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/integrations/youtube/connections/${encodeURIComponent(channelId)}`);
  }

  getCampaigns(): Observable<FunnelCampaign[]> {
    return this.http.get<FunnelCampaign[]>(`${this.api}/trafficfunnel/campaigns`).pipe(
      catchError(() => of([]))
    );
  }

  createCampaign(campaign: FunnelCampaign): Observable<FunnelCampaign> {
    return this.http.post<FunnelCampaign>(`${this.api}/trafficfunnel/campaigns`, campaign);
  }

  recordUserConversion(id: string): Observable<FunnelCampaign> {
    return this.http.post<FunnelCampaign>(`${this.api}/trafficfunnel/campaigns/${encodeURIComponent(id)}/convert`, {});
  }

  deleteUserCampaign(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/trafficfunnel/campaigns/${encodeURIComponent(id)}`);
  }

  trackClick(slug: string): Observable<{ success: boolean; destination: string; totalClicks: number }> {
    return this.http.post<{ success: boolean; destination: string; totalClicks: number }>(
      `${this.api}/trafficfunnel/track/${encodeURIComponent(slug)}`,
      {}
    );
  }

  // ---- Admin-Only APIs (Require JWT with Admin role) ----

  getAdminStatus(): Observable<AdminStatus> {
    return this.http.get<AdminStatus>(`${this.api}/admin/status`);
  }

  syncEcosystem(purgeLegacy = true): Observable<EcosystemSyncSummary> {
    return this.http.post<EcosystemSyncSummary>(`${this.api}/admin/sync/ecosystem?purgeLegacy=${purgeLegacy}`, {});
  }

  syncAllYouTube(): Observable<EcosystemSyncSummary> {
    return this.http.post<EcosystemSyncSummary>(`${this.api}/admin/sync/youtube`, {});
  }

  syncChannelLive(id: string): Observable<ChannelMetrics> {
    return this.http.post<ChannelMetrics>(`${this.api}/admin/channels/${encodeURIComponent(id)}/sync-live`, {});
  }

  purgeLegacyDemo(): Observable<{ purgedChannels: number }> {
    return this.http.post<{ purgedChannels: number }>(`${this.api}/admin/purge-legacy`, {});
  }

  getIntegrations(): Observable<IntegrationSettingsView> {
    return this.http.get<IntegrationSettingsView>(`${this.api}/admin/integrations`);
  }

  saveIntegrations(req: UpdateIntegrationSettingsRequest): Observable<IntegrationSettingsView> {
    return this.http.put<IntegrationSettingsView>(`${this.api}/admin/integrations`, req);
  }

  getAdminChannels(): Observable<ChannelMetrics[]> {
    return this.http.get<ChannelMetrics[]>(`${this.api}/admin/channels`);
  }

  saveChannel(channel: ChannelMetrics): Observable<ChannelMetrics> {
    return channel.id
      ? this.http.put<ChannelMetrics>(`${this.api}/admin/channels/${encodeURIComponent(channel.id)}`, channel)
      : this.http.post<ChannelMetrics>(`${this.api}/admin/channels`, channel);
  }

  deleteChannel(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/channels/${encodeURIComponent(id)}`);
  }

  getAdminFunnels(): Observable<FunnelCampaign[]> {
    return this.http.get<FunnelCampaign[]>(`${this.api}/admin/funnels`);
  }

  saveFunnel(campaign: FunnelCampaign): Observable<FunnelCampaign> {
    return campaign.id
      ? this.http.put<FunnelCampaign>(`${this.api}/admin/funnels/${encodeURIComponent(campaign.id)}`, campaign)
      : this.http.post<FunnelCampaign>(`${this.api}/admin/funnels`, campaign);
  }

  recordConversion(id: string): Observable<FunnelCampaign> {
    return this.http.post<FunnelCampaign>(`${this.api}/admin/funnels/${encodeURIComponent(id)}/convert`, {});
  }

  deleteFunnel(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/funnels/${encodeURIComponent(id)}`);
  }

  getAdminSuggestions(): Observable<GrowthSuggestion[]> {
    return this.http.get<GrowthSuggestion[]>(`${this.api}/admin/suggestions`);
  }

  saveSuggestion(suggestion: GrowthSuggestion): Observable<GrowthSuggestion> {
    return suggestion.id
      ? this.http.put<GrowthSuggestion>(`${this.api}/admin/suggestions/${encodeURIComponent(suggestion.id)}`, suggestion)
      : this.http.post<GrowthSuggestion>(`${this.api}/admin/suggestions`, suggestion);
  }

  deleteSuggestion(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/suggestions/${encodeURIComponent(id)}`);
  }

  saveGrowthConfig(config: GrowthConfig): Observable<GrowthConfig> {
    return this.http.put<GrowthConfig>(`${this.api}/admin/growth-config`, config);
  }

  saveEarningsConfig(config: EarningsConfig): Observable<EarningsConfig> {
    return this.http.put<EarningsConfig>(`${this.api}/admin/earnings-config`, config);
  }

  saveSiteConfig(config: SiteConfig): Observable<SiteConfig> {
    return this.http.put<SiteConfig>(`${this.api}/admin/site-config`, config);
  }

  private calculateLocalEarnings(req: EarningsCalculationRequest): EarningsReport {
    const adRev = (req.monthlyViews / 1000) * req.estimatedRpm;
    const sponRev = req.monthlySponsorshipDeals * req.avgSponsorshipRate;
    const affRev = req.monthlyViews * (req.affiliateConversionRatePercentage / 100) * req.avgAffiliatePayout;
    const total = adRev + sponRev + affRev;
    return {
      monthlyViews: req.monthlyViews,
      adRevenueMonthly: Math.round(adRev * 100) / 100,
      sponsorshipRevenueMonthly: Math.round(sponRev * 100) / 100,
      affiliateRevenueMonthly: Math.round(affRev * 100) / 100,
      totalEstimatedMonthlyRevenue: Math.round(total * 100) / 100,
      totalEstimatedYearlyRevenue: Math.round(total * 12 * 100) / 100,
      effectiveRpm: req.monthlyViews > 0 ? Math.round(((total / req.monthlyViews) * 1000) * 100) / 100 : 0,
      currency: 'USD',
      streams: [
        { streamName: 'Direct Ads / AdSense', amount: Math.round(adRev), percentageShare: total ? Math.round((adRev / total) * 100) : 0 },
        { streamName: 'Brand Sponsorships', amount: Math.round(sponRev), percentageShare: total ? Math.round((sponRev / total) * 100) : 0 },
        { streamName: 'Affiliate & Link Conversions', amount: Math.round(affRev), percentageShare: total ? Math.round((affRev / total) * 100) : 0 }
      ],
      monetizationOpportunities: []
    };
  }
}
