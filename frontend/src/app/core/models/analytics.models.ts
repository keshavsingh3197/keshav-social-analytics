export type SocialPlatform = 'YouTube' | 'Instagram' | 'Facebook' | 'TikTok' | 'LinkedIn' | 'Twitter';

export interface ChannelMetrics {
  id: string;
  channelName: string;
  channelHandle: string;
  platform: SocialPlatform;
  niche: string;
  followersOrSubscribers: number;
  totalViewsLast30Days: number;
  engagementRatePercentage: number;
  averageViewDurationMinutes: number;
  videosOrPostsCount: number;
  growthRateMonthlyPercentage: number;
  lastUpdated: string;
}

export interface PlatformSummary {
  platform: SocialPlatform;
  totalAudience: number;
  totalViews: number;
  avgEngagement: number;
  estimatedEarningsMonthly: number;
}

export interface EarningsCalculationRequest {
  platform: SocialPlatform;
  niche: string;
  monthlyViews: number;
  estimatedRpm: number;
  monthlySponsorshipDeals: number;
  avgSponsorshipRate: number;
  affiliateConversionRatePercentage: number;
  avgAffiliatePayout: number;
}

export interface RevenueStreamBreakdown {
  streamName: string;
  amount: number;
  percentageShare: number;
}

export interface EarningsReport {
  monthlyViews: number;
  adRevenueMonthly: number;
  sponsorshipRevenueMonthly: number;
  affiliateRevenueMonthly: number;
  totalEstimatedMonthlyRevenue: number;
  totalEstimatedYearlyRevenue: number;
  effectiveRpm: number;
  currency: string;
  streams: RevenueStreamBreakdown[];
  monetizationOpportunities: string[];
}

export interface GrowthSuggestion {
  id: string;
  category: string;
  title: string;
  description: string;
  impact: 'High' | 'Medium' | 'Low';
  actionItem: string;
  estimatedGain: string;
  applicablePlatform: SocialPlatform;
}

export interface ChannelAuditResult {
  channelHandle: string;
  healthScoreOutOf100: number;
  prioritySuggestions: GrowthSuggestion[];
  bestUploadTimes: string[];
  highDemandKeywords: string[];
}

export interface FunnelCampaign {
  id?: string;
  title: string;
  sourcePlatform: string;
  destinationUrl: string;
  trackingSlug: string;
  totalClicks: number;
  conversions: number;
  conversionRatePercentage: number;
  createdAt?: string;
  isActive: boolean;
}
