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
  rpmUsd?: number;
  estimatedMonthlyRevenue?: number;
  channelUrl?: string;
  isActive?: boolean;
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

export interface PlatformBenchmark {
  platform: string;
  icon: string;
  defaultMonthlyViews: number;
  defaultRpmUsd: number;
  defaultSponsorshipUsd: number;
  affiliateConversionRatePercent: number;
  avgOrderValueUsd: number;
  description: string;
}

export interface NichePreset {
  id: string;
  name: string;
  minRpmUsd: number;
  maxRpmUsd: number;
  suggestedRpmUsd: number;
  tip: string;
}

export interface MonetizationOpportunity {
  id: string;
  title: string;
  platform: string;
  impact: string;
  description: string;
  actionUrl: string;
}

export interface EarningsConfig {
  id?: string;
  platformBenchmarks: PlatformBenchmark[];
  niches: NichePreset[];
  opportunities: MonetizationOpportunity[];
  updatedAt?: string;
}

export interface GrowthConfig {
  id?: string;
  bestUploadTimes: Record<string, string>;
  highDemandKeywords: string[];
  targetEngagementPercent: number;
  targetAvgWatchMinutes: number;
  updatedAt?: string;
}

export interface SiteConfig {
  id?: string;
  heroTitle: string;
  heroSubtitle: string;
  youtubeSnippetHeader: string;
  youtubeSnippetFooter: string;
  hashtags: string;
  defaultDestinationBaseUrl: string;
  adminPortalUrl: string;
  updatedAt?: string;
}

export interface AdminStatus {
  storageMode: string;
  channelCount: number;
  funnelCount: number;
  suggestionCount: number;
  adminUser: string;
}

export interface EcosystemWebsite {
  key: string;
  name: string;
  url: string;
  icon?: string;
  description?: string;
  sortOrder: number;
  showInEcosystemBar: boolean;
  updatedAt?: string;
}

export interface AppConfigEnvelope {
  siteTitle?: string;
  blogUrl?: string;
  blogAdminUrl?: string;
  version?: string;
  defaultLocale?: string;
  values?: Record<string, string>;
  websites?: EcosystemWebsite[];
}
