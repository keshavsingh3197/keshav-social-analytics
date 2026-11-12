import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminStatus,
  ChannelAuditResult,
  ChannelMetrics,
  EarningsCalculationRequest,
  EarningsConfig,
  EarningsReport,
  FunnelCampaign,
  GrowthConfig,
  GrowthSuggestion,
  PlatformSummary,
  SiteConfig,
  SocialPlatform
} from '../models/analytics.models';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  readonly backendOffline = signal(false);

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

  getEarningsConfig(): Observable<EarningsConfig> {
    return this.http.get<EarningsConfig>(`${this.api}/earnings/config`).pipe(
      catchError(() => of(this.fallbackEarningsConfig()))
    );
  }

  calculateEarnings(req: EarningsCalculationRequest): Observable<EarningsReport> {
    return this.http.post<EarningsReport>(`${this.api}/earnings/calculate`, req).pipe(
      catchError(() => of(this.calculateLocalEarnings(req)))
    );
  }

  auditChannel(handle: string = '@KeshavSingh', platform: SocialPlatform = 'YouTube'): Observable<ChannelAuditResult> {
    return this.http
      .get<ChannelAuditResult>(`${this.api}/recommendations/audit?handle=${encodeURIComponent(handle)}&platform=${platform}`)
      .pipe(catchError(() => of({ channelHandle: handle, healthScoreOutOf100: 0, prioritySuggestions: [], bestUploadTimes: [], highDemandKeywords: [] })));
  }

  getGrowthConfig(): Observable<GrowthConfig> {
    return this.http.get<GrowthConfig>(`${this.api}/recommendations/config`);
  }

  getSiteConfig(): Observable<SiteConfig> {
    return this.http.get<SiteConfig>(`${this.api}/siteconfig`).pipe(
      catchError(() => of({
        heroTitle: 'Social Channel Performance & Growth Hub',
        heroSubtitle: 'Track multi-platform engagement across YouTube, Instagram, and Facebook. Maximize audience retention, calculate creator earnings, and funnel viewer traffic directly into your monetized ecosystems.',
        youtubeSnippetHeader: '🚀 Explore Keshav Singh Ecosystem Tools:',
        youtubeSnippetFooter: '🔔 Subscribe for weekly AI, engineering & cultural deep dives!',
        hashtags: '#KeshavSingh #AIAnimation #HinduPanchang #WebDev',
        defaultDestinationBaseUrl: 'https://studio.keshavsingh.in',
        adminPortalUrl: 'https://admin.keshavsingh.in'
      }))
    );
  }

  getCampaigns(): Observable<FunnelCampaign[]> {
    return this.http.get<FunnelCampaign[]>(`${this.api}/trafficfunnel/campaigns`).pipe(
      catchError(() => of([]))
    );
  }

  createCampaign(campaign: FunnelCampaign): Observable<FunnelCampaign> {
    return this.http.post<FunnelCampaign>(`${this.api}/trafficfunnel/campaigns`, campaign);
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

  private fallbackEarningsConfig(): EarningsConfig {
    return {
      platformBenchmarks: [
        { platform: 'YouTube', icon: '📺', defaultMonthlyViews: 384000, defaultRpmUsd: 3.78, defaultSponsorshipUsd: 500, affiliateConversionRatePercent: 0.6, avgOrderValueUsd: 18, description: 'Long-form YouTube videos' },
        { platform: 'Instagram', icon: '📸', defaultMonthlyViews: 512000, defaultRpmUsd: 1.66, defaultSponsorshipUsd: 350, affiliateConversionRatePercent: 0.9, avgOrderValueUsd: 15, description: 'Instagram Reels' },
        { platform: 'Facebook', icon: '📘', defaultMonthlyViews: 180000, defaultRpmUsd: 2.33, defaultSponsorshipUsd: 200, affiliateConversionRatePercent: 0.5, avgOrderValueUsd: 12, description: 'Facebook Videos' }
      ],
      niches: [
        { id: 'n-1', name: 'Tech, AI & Software Engineering', minRpmUsd: 3.5, maxRpmUsd: 9.0, suggestedRpmUsd: 5.2, tip: 'High SaaS advertiser bids.' }
      ],
      opportunities: []
    };
  }
}
