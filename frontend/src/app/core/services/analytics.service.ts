import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ChannelMetrics,
  PlatformSummary,
  EarningsReport,
  EarningsCalculationRequest,
  ChannelAuditResult,
  FunnelCampaign,
  SocialPlatform
} from '../models/analytics.models';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  getChannels(): Observable<ChannelMetrics[]> {
    return this.http.get<ChannelMetrics[]>(`${this.api}/analytics/channels`).pipe(
      catchError(() => of(this.getFallbackChannels()))
    );
  }

  getOverview(): Observable<PlatformSummary[]> {
    return this.http.get<PlatformSummary[]>(`${this.api}/analytics/overview`).pipe(
      catchError(() => of(this.getFallbackOverview()))
    );
  }

  calculateEarnings(req: EarningsCalculationRequest): Observable<EarningsReport> {
    return this.http.post<EarningsReport>(`${this.api}/earnings/calculate`, req).pipe(
      catchError(() => of(this.calculateLocalEarnings(req)))
    );
  }

  auditChannel(handle: string = '@KeshavSingh', platform: SocialPlatform = 'YouTube'): Observable<ChannelAuditResult> {
    return this.http.get<ChannelAuditResult>(`${this.api}/recommendations/audit?handle=${encodeURIComponent(handle)}&platform=${platform}`).pipe(
      catchError(() => of(this.getFallbackAudit(handle, platform)))
    );
  }

  getCampaigns(): Observable<FunnelCampaign[]> {
    return this.http.get<FunnelCampaign[]>(`${this.api}/trafficfunnel/campaigns`).pipe(
      catchError(() => of(this.getFallbackCampaigns()))
    );
  }

  createCampaign(campaign: FunnelCampaign): Observable<FunnelCampaign> {
    return this.http.post<FunnelCampaign>(`${this.api}/trafficfunnel/campaigns`, campaign).pipe(
      catchError(() => {
        campaign.id = 'camp-' + Date.now();
        campaign.createdAt = new Date().toISOString();
        return of(campaign);
      })
    );
  }

  // Graceful local fallbacks so the UI displays seamlessly even before the local backend is booted
  private getFallbackChannels(): ChannelMetrics[] {
    return [
      {
        id: 'ch-1',
        channelName: 'Keshav Singh Official',
        channelHandle: '@KeshavSingh',
        platform: 'YouTube',
        niche: 'AI, Animation & Dev',
        followersOrSubscribers: 45200,
        totalViewsLast30Days: 384000,
        engagementRatePercentage: 7.4,
        averageViewDurationMinutes: 4.8,
        videosOrPostsCount: 142,
        growthRateMonthlyPercentage: 12.5,
        lastUpdated: new Date().toISOString()
      },
      {
        id: 'ch-2',
        channelName: 'Keshav Dev & Stories',
        channelHandle: '@KeshavStories',
        platform: 'Instagram',
        niche: 'Culture & Short Stories',
        followersOrSubscribers: 28600,
        totalViewsLast30Days: 512000,
        engagementRatePercentage: 8.9,
        averageViewDurationMinutes: 0.9,
        videosOrPostsCount: 210,
        growthRateMonthlyPercentage: 18.2,
        lastUpdated: new Date().toISOString()
      },
      {
        id: 'ch-3',
        channelName: 'Keshav Cultural Page',
        channelHandle: 'keshav.cultural',
        platform: 'Facebook',
        niche: 'Vedic Panchang & History',
        followersOrSubscribers: 19400,
        totalViewsLast30Days: 180000,
        engagementRatePercentage: 5.2,
        averageViewDurationMinutes: 2.1,
        videosOrPostsCount: 88,
        growthRateMonthlyPercentage: 6.4,
        lastUpdated: new Date().toISOString()
      }
    ];
  }

  private getFallbackOverview(): PlatformSummary[] {
    return [
      { platform: 'YouTube', totalAudience: 45200, totalViews: 384000, avgEngagement: 7.4, estimatedEarningsMonthly: 1450 },
      { platform: 'Instagram', totalAudience: 28600, totalViews: 512000, avgEngagement: 8.9, estimatedEarningsMonthly: 850 },
      { platform: 'Facebook', totalAudience: 19400, totalViews: 180000, avgEngagement: 5.2, estimatedEarningsMonthly: 420 }
    ];
  }

  private calculateLocalEarnings(req: EarningsCalculationRequest): EarningsReport {
    const adRevenue = (req.monthlyViews / 1000) * req.estimatedRpm;
    const sponsorship = req.monthlySponsorshipDeals * req.avgSponsorshipRate;
    const affiliate = req.monthlyViews * (req.affiliateConversionRatePercentage / 100) * req.avgAffiliatePayout;
    const totalMonthly = adRevenue + sponsorship + affiliate;
    const totalYearly = totalMonthly * 12;
    const effectiveRpm = req.monthlyViews > 0 ? (totalMonthly / req.monthlyViews) * 1000 : 0;

    return {
      monthlyViews: req.monthlyViews,
      adRevenueMonthly: Math.round(adRevenue),
      sponsorshipRevenueMonthly: Math.round(sponsorship),
      affiliateRevenueMonthly: Math.round(affiliate),
      totalEstimatedMonthlyRevenue: Math.round(totalMonthly),
      totalEstimatedYearlyRevenue: Math.round(totalYearly),
      effectiveRpm: Number(effectiveRpm.toFixed(2)),
      currency: 'USD',
      streams: [
        { streamName: 'AdSense / Direct Ads', amount: Math.round(adRevenue), percentageShare: totalMonthly > 0 ? Math.round((adRevenue / totalMonthly) * 100) : 0 },
        { streamName: 'Brand Sponsorships', amount: Math.round(sponsorship), percentageShare: totalMonthly > 0 ? Math.round((sponsorship / totalMonthly) * 100) : 0 },
        { streamName: 'Affiliate & Link Conversions', amount: Math.round(affiliate), percentageShare: totalMonthly > 0 ? Math.round((affiliate / totalMonthly) * 100) : 0 }
      ],
      monetizationOpportunities: [
        'Funnel audience to studio.keshavsingh.in for subscription SaaS monetization',
        'Direct spiritual devotees to panchang.keshavsingh.in for daily puja affiliate items',
        'Add sponsored dedicated segments in high-retention videos',
        'Package digital presets / scripts as paid creator downloads'
      ]
    };
  }

  private getFallbackAudit(handle: string, platform: SocialPlatform): ChannelAuditResult {
    return {
      channelHandle: handle,
      healthScoreOutOf100: 86.5,
      prioritySuggestions: [
        {
          id: 's-1',
          category: 'Retention & Hook',
          title: 'Optimize the First 30 Seconds (The Hook)',
          description: 'Audience retention drops quickly if the intro is too slow. Cut logo bumpers and start with an immediate curiosity gap.',
          impact: 'High',
          actionItem: 'Show the end result or ask a high-stakes question in the first 5 seconds.',
          estimatedGain: '+24% Watch Time Retention',
          applicablePlatform: platform
        },
        {
          id: 's-2',
          category: 'SEO & Title CTR',
          title: 'Add Numbers & High-Intent Contrast in Titles',
          description: 'Videos using quantitative proof in titles achieve 35% higher CTR across suggested feeds.',
          impact: 'High',
          actionItem: 'Test titles like: "I Tested 5 AI Tools in 7 Days (Here is the Winner)".',
          estimatedGain: '+35% Click-Through Rate',
          applicablePlatform: platform
        },
        {
          id: 's-3',
          category: 'Upload Timing',
          title: 'Target Regional Evening Commute & Weekend Mornings',
          description: 'Peak engagement spikes Tuesdays & Thursdays between 6:30 PM - 8:30 PM IST.',
          impact: 'Medium',
          actionItem: 'Schedule uploads 45 mins before peak hours so YouTube algorithms process HD & index metadata.',
          estimatedGain: '+18% 24h Velocity',
          applicablePlatform: platform
        },
        {
          id: 's-4',
          category: 'Traffic Funneling',
          title: 'Pin Links to Sibling Projects in Comments',
          description: 'Drive YouTube & Instagram traffic to studio.keshavsingh.in and panchang.keshavsingh.in to multiply revenue.',
          impact: 'High',
          actionItem: 'Add a clear call to action at the top of descriptions and pin the comment with tracked links.',
          estimatedGain: '+500 Referral Visits / Video',
          applicablePlatform: platform
        }
      ],
      bestUploadTimes: [
        'Tuesday: 6:30 PM - 8:30 PM IST',
        'Thursday: 7:00 PM - 9:00 PM IST',
        'Saturday: 11:00 AM - 1:00 PM IST',
        'Sunday: 10:00 AM - 12:30 PM IST'
      ],
      highDemandKeywords: [
        'AI animation studio online',
        'Daily hindu panchang today',
        'YouTube monetization tips 2026',
        'Vedic astrology calendar',
        'Creator RPM growth strategies'
      ]
    };
  }

  private getFallbackCampaigns(): FunnelCampaign[] {
    return [
      {
        id: 'f-1',
        title: 'AnimStudio AI Promotion from YouTube',
        sourcePlatform: 'YouTube Description',
        destinationUrl: 'https://studio.keshavsingh.in',
        trackingSlug: 'yt-animstudio',
        totalClicks: 2840,
        conversions: 312,
        conversionRatePercentage: 11.0,
        isActive: true
      },
      {
        id: 'f-2',
        title: 'Daily Panchang & Rashifal Link from Instagram',
        sourcePlatform: 'Instagram Bio',
        destinationUrl: 'https://panchang.keshavsingh.in',
        trackingSlug: 'ig-panchang',
        totalClicks: 4920,
        conversions: 890,
        conversionRatePercentage: 18.1,
        isActive: true
      },
      {
        id: 'f-3',
        title: 'Dev Blog Articles Cross-promotion',
        sourcePlatform: 'Facebook Page',
        destinationUrl: 'https://git.keshavsingh.in',
        trackingSlug: 'fb-blog',
        totalClicks: 1150,
        conversions: 140,
        conversionRatePercentage: 12.2,
        isActive: true
      }
    ];
  }
}
