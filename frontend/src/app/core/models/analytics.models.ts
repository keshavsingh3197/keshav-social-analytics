export type SocialPlatform = 'YouTube' | 'Instagram' | 'Facebook' | 'TikTok' | 'LinkedIn' | 'Twitter';

export interface RecentVideoItem {
  videoId: string;
  title: string;
  url: string;
  thumbnailUrl: string;
  publishedAt: string;
  views: number;
  descriptionSnippet?: string;
}

export interface RecentFeedVideo extends RecentVideoItem {
  channelId: string;
  channelName: string;
  channelHandle: string;
}

export interface ChannelMetrics {
  id: string;
  channelName: string;
  channelHandle: string;
  platform: SocialPlatform;
  niche: string;
  externalChannelId?: string;
  avatarUrl?: string;
  dataSource?: string;
  isVerifiedLive?: boolean;
  lastSyncedAt?: string;
  followersOrSubscribers: number;
  totalViewsLast30Days: number;
  engagementRatePercentage: number;
  averageViewDurationMinutes: number;
  videosOrPostsCount: number;
  growthRateMonthlyPercentage: number;
  rpmUsd?: number;
  estimatedMonthlyRevenue?: number;
  channelUrl?: string;
  recentVideos?: RecentVideoItem[];
  ownerUserId?: string;
  isAdminOwned?: boolean;
  isActive?: boolean;
  lastUpdated: string;
}

export interface PlatformSummary {
  platform: SocialPlatform;
  totalAudience: number;
  totalViews: number;
  avgEngagement: number;
  estimatedEarningsMonthly: number;
  channelCount?: number;
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
  ownerUserId?: string;
  isAdminOwned?: boolean;
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

export interface AdMonetizationConfig {
  adsEnabled: boolean;
  adSenseClientId: string;
  headerAdSlotId: string;
  inFeedAdSlotId: string;
  sidebarAdSlotId: string;
  footerAdSlotId: string;
  adLabel: string;
  sponsorBannerEnabled: boolean;
  sponsorBadge: string;
  sponsorTitle: string;
  sponsorDescription: string;
  sponsorCtaText: string;
  sponsorUrl: string;
}

export interface SiteConfig {
  id?: string;
  brandName?: string;
  brandSubtitle?: string;
  heroTitle: string;
  heroSubtitle: string;
  youtubeSnippetHeader: string;
  youtubeSnippetFooter: string;
  hashtags: string;
  defaultDestinationBaseUrl: string;
  ecosystemConfigUrl?: string;
  adminPortalUrl: string;
  ads?: AdMonetizationConfig;
  updatedAt?: string;
}

export interface IntegrationSettingsView {
  hasYouTubeApiKey: boolean;
  youTubeApiKeyMasked?: string | null;
  hasGoogleClientSecret?: boolean;
  googleClientSecretMasked?: string | null;
  googleClientId?: string | null;
  oAuthRedirectUri?: string | null;
  isYouTubeOAuthConfigured?: boolean;
  managedByCentralAdmin?: boolean;
  centralYouTubeApiKeyConfigured?: boolean;
  centralGoogleOAuthConfigured?: boolean;
  centralAdSenseConfigured?: boolean;
  adSenseClientId?: string | null;
  ecosystemConfigUrl: string;
  adminPortalUrl: string;
  updatedAt?: string;
}

export interface UpdateIntegrationSettingsRequest {
  youTubeApiKey?: string;
  clearYouTubeApiKey?: boolean;
  googleClientId?: string;
  googleClientSecret?: string;
  clearGoogleOAuth?: boolean;
  oAuthRedirectUri?: string;
  ecosystemConfigUrl?: string;
  adminPortalUrl?: string;
}

export interface ChannelSearchCandidate {
  externalChannelId: string;
  channelName: string;
  channelHandle: string;
  platform: string;
  avatarUrl: string;
  followersOrSubscribers: number;
  videoCount: number;
  description: string;
  channelUrl: string;
  dataSource: string;
  isVerified: boolean;
}

export interface PublicChannelLookupRequest {
  query: string;
  platform: string;
  niche?: string;
}

export interface PublicChannelAnalysisResult {
  channel: ChannelMetrics;
  earnings: EarningsReport;
  audit: ChannelAuditResult;
  autoInsights: string[];
  resolvedLive: boolean;
  resolutionSummary: string;
}

export interface YouTubeConnectionView {
  channelId: string;
  channelTitle: string;
  channelHandle?: string | null;
  avatarUrl?: string | null;
  connectedAtUtc: string;
  lastSyncedAtUtc?: string | null;
  needsReconnect: boolean;
}

export interface YouTubeOAuthConfigPublicView {
  isOAuthConfigured: boolean;
  isApiKeyConfigured: boolean;
  redirectUriHint: string;
}

export interface YouTubeOAuthStatusResponse {
  isOAuthConfigured: boolean;
  connections: YouTubeConnectionView[];
}

export interface YouTubeOAuthCompleteResponse {
  connection: YouTubeConnectionView;
  channel: ChannelMetrics;
  returnPath: string;
}

export interface EcosystemSyncSummary {
  importedChannels: number;
  updatedChannels: number;
  liveVideosFetched: number;
  funnelsCreated: number;
  purgedLegacyRecords: number;
  usedYouTubeApiKey: boolean;
  message: string;
  syncedAt: string;
}

export interface AdminStatus {
  storageMode: string;
  channelCount: number;
  verifiedLiveChannels?: number;
  recentVideoCount?: number;
  funnelCount: number;
  suggestionCount: number;
  hasYouTubeApiKey?: boolean;
  adminUser: string;
}

export interface CentralChannel {
  id?: string;
  key: string;
  name: string;
  handle: string;
  platform: string;
  channelId?: string;
  url: string;
  niche?: string;
  tagline?: string;
  description?: string;
  hashtags?: string[];
  showInAnalytics?: boolean;
  showInFooter?: boolean;
  isPrimary?: boolean;
  isEnabled?: boolean;
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
  channels?: CentralChannel[];
}
