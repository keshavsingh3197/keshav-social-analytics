import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { AnalyticsService } from '../../core/services/analytics.service';
import { EcosystemConfigService } from '../../core/services/ecosystem-config.service';
import {
  AdMonetizationConfig,
  AdminStatus,
  ChannelMetrics,
  EarningsConfig,
  FunnelCampaign,
  GrowthConfig,
  GrowthSuggestion,
  IntegrationSettingsView,
  SiteConfig,
  SocialPlatform
} from '../../core/models/analytics.models';

type AdminTab = 'channels' | 'funnels' | 'growth' | 'earnings' | 'site';

@Component({
  selector: 'app-social-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="admin-page">
      @if (!auth.isAdmin()) {
        <div class="denied-card">
          <h1>🛡️ Admin Access Required</h1>
          <p>Only users holding the <strong>Admin</strong> role can access Social Analytics management.</p>
          <button (click)="auth.loginRedirect()" class="btn-primary">Sign in with SSO</button>
        </div>
      } @else {
        <!-- Top Admin Banner -->
        <section class="admin-hero">
          <div>
            <span class="admin-chip">🛡️ Admin-Only Control Plane</span>
            <h1>SocialPulse Dynamic Content & Analytics Admin</h1>
            <p>
              Manage channels, affiliate funnel targets, growth rules, RPM presets &amp; site copy.
              @if (status(); as st) {
                Storage: <strong>{{ st.storageMode }}</strong> • Signed in as <strong>{{ st.adminUser }}</strong>
              }
            </p>
          </div>
          <div class="hero-actions">
            <a [href]="ecosystem.adminPortalUrl() + '/website'" target="_blank" rel="noopener" class="btn-outline">
              🌐 Global Website Registry
            </a>
            <a [href]="ecosystem.adminPortalUrl() + '/settings'" target="_blank" rel="noopener" class="btn-outline">
              ⚙️ Central App Config
            </a>
          </div>
        </section>

        @if (feedback(); as fb) {
          <div class="feedback-bar" [class.error]="fb.isError">
            <span>{{ fb.message }}</span>
            <button type="button" (click)="feedback.set(null)">✕</button>
          </div>
        }

        <!-- Tab Switcher -->
        <nav class="tab-bar">
          <button [class.active]="activeTab() === 'channels'" (click)="activeTab.set('channels')">
            📺 Channels ({{ channels().length }})
          </button>
          <button [class.active]="activeTab() === 'funnels'" (click)="activeTab.set('funnels')">
            🎯 Funnels ({{ funnels().length }})
          </button>
          <button [class.active]="activeTab() === 'growth'" (click)="activeTab.set('growth')">
            🚀 Growth Advisor ({{ suggestions().length }})
          </button>
          <button [class.active]="activeTab() === 'earnings'" (click)="activeTab.set('earnings')">
            💰 Earnings Benchmarks
          </button>
          <button [class.active]="activeTab() === 'site'" (click)="activeTab.set('site')">
            ⚙️ Site &amp; Global Ecosystem
          </button>
        </nav>

        <!-- TAB 1: CHANNELS CRUD -->
        @if (activeTab() === 'channels') {
          <section class="panel sync-panel">
            <div class="row-between">
              <div>
                <h2>⚡ Central Ecosystem &amp; Live YouTube Sync</h2>
                <p class="muted">
                  Import analytics channels &amp; websites from Central Admin and fetch live YouTube RSS/API video counts &amp; views.
                </p>
              </div>
              <div class="btn-row">
                <button (click)="runSyncEcosystem(true)" [disabled]="syncing()" class="btn-primary">
                  {{ syncing() ? '⏳ Syncing…' : '🔄 Sync Central Channels & YouTube Feeds' }}
                </button>
                <button (click)="runRefreshYouTube()" [disabled]="syncing()" class="btn-secondary">
                  📺 Refresh All YouTube Live
                </button>
                <button (click)="runPurgeLegacy()" [disabled]="syncing()" class="btn-sm danger">
                  🧹 Purge Legacy Demo Rows
                </button>
              </div>
            </div>
          </section>

          <section class="panel">
            <h2>{{ channelForm.id ? 'Edit Channel' : 'Add Social Channel / Page' }}</h2>
            <div class="form-grid">
              <div class="field">
                <label>Channel Name</label>
                <input [(ngModel)]="channelForm.channelName" placeholder="Channel display name" />
              </div>
              <div class="field">
                <label>Handle / Slug</label>
                <input [(ngModel)]="channelForm.channelHandle" placeholder="@handle" />
              </div>
              <div class="field">
                <label>Platform</label>
                <select [(ngModel)]="channelForm.platform">
                  <option value="YouTube">YouTube</option>
                  <option value="Instagram">Instagram</option>
                  <option value="Facebook">Facebook</option>
                  <option value="LinkedIn">LinkedIn</option>
                  <option value="TikTok">TikTok</option>
                  <option value="Twitter">Twitter</option>
                </select>
              </div>
              <div class="field">
                <label>Niche</label>
                <input [(ngModel)]="channelForm.niche" placeholder="AI, Animation & Dev" />
              </div>
              <div class="field">
                <label>Subscribers / Followers</label>
                <input type="number" [(ngModel)]="channelForm.followersOrSubscribers" />
              </div>
              <div class="field">
                <label>30-Day Views</label>
                <input type="number" [(ngModel)]="channelForm.totalViewsLast30Days" />
              </div>
              <div class="field">
                <label>Engagement (%)</label>
                <input type="number" step="0.1" [(ngModel)]="channelForm.engagementRatePercentage" />
              </div>
              <div class="field">
                <label>Avg Watch Duration (mins)</label>
                <input type="number" step="0.1" [(ngModel)]="channelForm.averageViewDurationMinutes" />
              </div>
              <div class="field">
                <label>Posts / Videos</label>
                <input type="number" [(ngModel)]="channelForm.videosOrPostsCount" />
              </div>
              <div class="field">
                <label>Monthly Growth (%)</label>
                <input type="number" step="0.1" [(ngModel)]="channelForm.growthRateMonthlyPercentage" />
              </div>
              <div class="field">
                <label>RPM (USD)</label>
                <input type="number" step="0.01" [(ngModel)]="channelForm.rpmUsd" />
              </div>
              <div class="field">
                <label>Est. Monthly Revenue (USD)</label>
                <input type="number" step="0.01" [(ngModel)]="channelForm.estimatedMonthlyRevenue" />
              </div>
              <div class="field field-wide">
                <label>Channel URL</label>
                <input [(ngModel)]="channelForm.channelUrl" placeholder="https://www.youtube.com/@handle" />
              </div>
              <div class="field checkbox-field">
                <label>
                  <input type="checkbox" [(ngModel)]="channelForm.isActive" /> Active on Public Dashboard
                </label>
              </div>
            </div>
            <div class="btn-row">
              <button (click)="saveChannel()" class="btn-primary">💾 Save Channel</button>
              <button (click)="resetChannelForm()" class="btn-secondary">Reset Form</button>
            </div>

            <div class="table-wrap">
              <table class="admin-table">
                <thead>
                  <tr>
                    <th>Channel</th>
                    <th>Platform / Source</th>
                    <th>Views (30d)</th>
                    <th>Followers</th>
                    <th>Live Videos</th>
                    <th>Est. Rev</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (ch of channels(); track ch.id) {
                    <tr>
                      <td><strong>{{ ch.channelName }}</strong><br><small>{{ ch.channelHandle }}</small></td>
                      <td>{{ ch.platform }}<br><small>{{ ch.dataSource || 'Admin' }}</small></td>
                      <td>{{ ch.totalViewsLast30Days | number }}</td>
                      <td>{{ ch.followersOrSubscribers | number }}</td>
                      <td>{{ ch.recentVideos?.length || ch.videosOrPostsCount }}</td>
                      <td>\${{ ch.estimatedMonthlyRevenue || 0 | number }}</td>
                      <td><span class="pill" [class.inactive]="ch.isActive === false">{{ ch.isActive === false ? 'Hidden' : 'Active' }}</span></td>
                      <td class="act-cell">
                        @if (ch.platform === 'YouTube') {
                          <button (click)="syncSingleChannel(ch.id)" class="btn-sm">⚡ Sync</button>
                        }
                        <button (click)="editChannel(ch)" class="btn-sm">Edit</button>
                        <button (click)="removeChannel(ch.id)" class="btn-sm danger">Delete</button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </section>
        }

        <!-- TAB 2: TRAFFIC FUNNELS CRUD -->
        @if (activeTab() === 'funnels') {
          <section class="panel">
            <h2>{{ funnelForm.id ? 'Edit Funnel Campaign' : 'Create Traffic Funnel' }}</h2>
            <div class="form-grid">
              <div class="field">
                <label>Campaign Title</label>
                <input [(ngModel)]="funnelForm.title" placeholder="AnimStudio AI Promotion" />
              </div>
              <div class="field">
                <label>Source Platform</label>
                <input [(ngModel)]="funnelForm.sourcePlatform" placeholder="YouTube Description" />
              </div>
              <div class="field">
                <label>Destination URL</label>
                <input [(ngModel)]="funnelForm.destinationUrl" placeholder="https://..." />
              </div>
              <div class="field">
                <label>Tracking Slug</label>
                <input [(ngModel)]="funnelForm.trackingSlug" placeholder="campaign-slug" />
              </div>
              <div class="field">
                <label>Total Clicks</label>
                <input type="number" [(ngModel)]="funnelForm.totalClicks" />
              </div>
              <div class="field">
                <label>Conversions</label>
                <input type="number" [(ngModel)]="funnelForm.conversions" />
              </div>
              <div class="field checkbox-field">
                <label><input type="checkbox" [(ngModel)]="funnelForm.isActive" /> Active Campaign</label>
              </div>
            </div>
            <div class="btn-row">
              <button (click)="saveFunnel()" class="btn-primary">💾 Save Funnel</button>
              <button (click)="resetFunnelForm()" class="btn-secondary">Reset Form</button>
            </div>

            <div class="table-wrap">
              <table class="admin-table">
                <thead>
                  <tr>
                    <th>Title</th>
                    <th>Slug</th>
                    <th>Target</th>
                    <th>Clicks / Conv</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (f of funnels(); track f.id) {
                    <tr>
                      <td><strong>{{ f.title }}</strong><br><small>{{ f.sourcePlatform }}</small></td>
                      <td><code>/t/{{ f.trackingSlug }}</code></td>
                      <td><small>{{ f.destinationUrl }}</small></td>
                      <td>{{ f.totalClicks | number }} / {{ f.conversions | number }} ({{ f.conversionRatePercentage }}%)</td>
                      <td><span class="pill" [class.inactive]="!f.isActive">{{ f.isActive ? 'Active' : 'Paused' }}</span></td>
                      <td class="act-cell">
                        <button (click)="addConversion(f.id!)" class="btn-sm">+1 Conv</button>
                        <button (click)="editFunnel(f)" class="btn-sm">Edit</button>
                        <button (click)="removeFunnel(f.id!)" class="btn-sm danger">Delete</button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </section>
        }

        <!-- TAB 3: GROWTH ADVISOR & CONFIG -->
        @if (activeTab() === 'growth') {
          <section class="panel">
            <h2>{{ suggestionForm.id ? 'Edit Recommendation Rule' : 'Add Growth Recommendation Rule' }}</h2>
            <div class="form-grid">
              <div class="field">
                <label>Category</label>
                <input [(ngModel)]="suggestionForm.category" placeholder="Retention & Drop-off Fix" />
              </div>
              <div class="field">
                <label>Title</label>
                <input [(ngModel)]="suggestionForm.title" placeholder="Optimize First 30 Seconds" />
              </div>
              <div class="field">
                <label>Impact</label>
                <select [(ngModel)]="suggestionForm.impact">
                  <option value="High">High</option>
                  <option value="Medium">Medium</option>
                  <option value="Low">Low</option>
                </select>
              </div>
              <div class="field">
                <label>Applicable Platform</label>
                <select [(ngModel)]="suggestionForm.applicablePlatform">
                  <option value="YouTube">YouTube</option>
                  <option value="Instagram">Instagram</option>
                  <option value="Facebook">Facebook</option>
                  <option value="LinkedIn">LinkedIn</option>
                  <option value="TikTok">TikTok</option>
                </select>
              </div>
              <div class="field">
                <label>Estimated Gain</label>
                <input [(ngModel)]="suggestionForm.estimatedGain" placeholder="+24% Retention" />
              </div>
              <div class="field field-wide">
                <label>Description</label>
                <input [(ngModel)]="suggestionForm.description" />
              </div>
              <div class="field field-wide">
                <label>Action Item</label>
                <input [(ngModel)]="suggestionForm.actionItem" />
              </div>
            </div>
            <div class="btn-row">
              <button (click)="saveSuggestion()" class="btn-primary">💾 Save Suggestion Rule</button>
              <button (click)="resetSuggestionForm()" class="btn-secondary">Reset</button>
            </div>

            <div class="table-wrap">
              <table class="admin-table">
                <thead>
                  <tr>
                    <th>Title</th>
                    <th>Platform</th>
                    <th>Impact</th>
                    <th>Gain</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (s of suggestions(); track s.id) {
                    <tr>
                      <td><strong>{{ s.title }}</strong><br><small>{{ s.category }}</small></td>
                      <td>{{ s.applicablePlatform }}</td>
                      <td>{{ s.impact }}</td>
                      <td>{{ s.estimatedGain }}</td>
                      <td class="act-cell">
                        <button (click)="editSuggestion(s)" class="btn-sm">Edit</button>
                        <button (click)="removeSuggestion(s.id)" class="btn-sm danger">Delete</button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <hr class="divider" />
            <h2>Algorithmic Targets, Upload Times &amp; Keywords</h2>
            <div class="form-grid">
              <div class="field">
                <label>Target Engagement (%)</label>
                <input type="number" step="0.1" [(ngModel)]="growthConfigForm.targetEngagementPercent" />
              </div>
              <div class="field">
                <label>Target Avg Watch Duration (mins)</label>
                <input type="number" step="0.1" [(ngModel)]="growthConfigForm.targetAvgWatchMinutes" />
              </div>
              <div class="field field-wide">
                <label>High-Demand Keywords (comma separated)</label>
                <input [(ngModel)]="keywordsCsv" placeholder="AI animation tools, Hindu panchang today" />
              </div>
              @for (entry of uploadTimeEntries; track entry.platform) {
                <div class="field">
                  <label>{{ entry.platform }} Best Time Window</label>
                  <input [(ngModel)]="entry.window" />
                </div>
              }
            </div>
            <div class="btn-row">
              <button (click)="saveGrowthConfig()" class="btn-primary">💾 Save Advisor Config</button>
            </div>
          </section>
        }

        <!-- TAB 4: EARNINGS BENCHMARKS & NICHES -->
        @if (activeTab() === 'earnings') {
          <section class="panel">
            <h2>Platform Calculator Benchmarks</h2>
            <div class="stack">
              @for (b of earningsForm.platformBenchmarks; track $index) {
                <div class="subcard">
                  <div class="form-grid">
                    <div class="field"><label>Platform</label><input [(ngModel)]="b.platform" /></div>
                    <div class="field"><label>Icon</label><input [(ngModel)]="b.icon" /></div>
                    <div class="field"><label>Default Views</label><input type="number" [(ngModel)]="b.defaultMonthlyViews" /></div>
                    <div class="field"><label>Default RPM ($)</label><input type="number" step="0.01" [(ngModel)]="b.defaultRpmUsd" /></div>
                    <div class="field"><label>Default Sponsor ($)</label><input type="number" [(ngModel)]="b.defaultSponsorshipUsd" /></div>
                    <div class="field"><label>Affiliate Conv (%)</label><input type="number" step="0.1" [(ngModel)]="b.affiliateConversionRatePercent" /></div>
                  </div>
                </div>
              }
            </div>

            <hr class="divider" />
            <div class="row-between">
              <h2>Content Niches &amp; RPM Ranges</h2>
              <button (click)="addNiche()" class="btn-sm">+ Add Niche</button>
            </div>
            <div class="stack">
              @for (n of earningsForm.niches; track $index) {
                <div class="subcard">
                  <div class="form-grid">
                    <div class="field"><label>Niche Name</label><input [(ngModel)]="n.name" /></div>
                    <div class="field"><label>Min RPM ($)</label><input type="number" step="0.1" [(ngModel)]="n.minRpmUsd" /></div>
                    <div class="field"><label>Max RPM ($)</label><input type="number" step="0.1" [(ngModel)]="n.maxRpmUsd" /></div>
                    <div class="field"><label>Suggested RPM ($)</label><input type="number" step="0.1" [(ngModel)]="n.suggestedRpmUsd" /></div>
                    <div class="field field-wide"><label>Tip</label><input [(ngModel)]="n.tip" /></div>
                  </div>
                  <button (click)="removeNiche($index)" class="btn-sm danger">Remove Niche</button>
                </div>
              }
            </div>

            <hr class="divider" />
            <div class="row-between">
              <h2>Monetization Opportunities</h2>
              <button (click)="addOpportunity()" class="btn-sm">+ Add Opportunity</button>
            </div>
            <div class="stack">
              @for (opp of earningsForm.opportunities; track $index) {
                <div class="subcard">
                  <div class="form-grid">
                    <div class="field"><label>Title</label><input [(ngModel)]="opp.title" /></div>
                    <div class="field"><label>Platform (or All)</label><input [(ngModel)]="opp.platform" /></div>
                    <div class="field"><label>Action URL</label><input [(ngModel)]="opp.actionUrl" /></div>
                    <div class="field field-wide"><label>Description</label><input [(ngModel)]="opp.description" /></div>
                  </div>
                  <button (click)="removeOpportunity($index)" class="btn-sm danger">Remove</button>
                </div>
              }
            </div>

            <div class="btn-row">
              <button (click)="saveEarningsConfig()" class="btn-primary">💾 Save All Earnings Benchmarks</button>
            </div>
          </section>
        }

        <!-- TAB 5: SITE CONFIG & CENTRAL GLOBAL ECOSYSTEM -->
        @if (activeTab() === 'site') {
          <section class="panel">
            <h2>SocialPulse App Settings</h2>
            <div class="form-grid">
              <div class="field field-wide"><label>Hero Title</label><input [(ngModel)]="siteForm.heroTitle" /></div>
              <div class="field field-wide"><label>Hero Subtitle</label><textarea rows="3" [(ngModel)]="siteForm.heroSubtitle"></textarea></div>
              <div class="field"><label>YouTube Snippet Header</label><input [(ngModel)]="siteForm.youtubeSnippetHeader" /></div>
              <div class="field"><label>YouTube Snippet Footer</label><input [(ngModel)]="siteForm.youtubeSnippetFooter" /></div>
              <div class="field"><label>Default Hashtags</label><input [(ngModel)]="siteForm.hashtags" /></div>
              <div class="field"><label>Default Funnel Destination</label><input [(ngModel)]="siteForm.defaultDestinationBaseUrl" /></div>
            </div>
            <div class="btn-row">
              <button (click)="saveSiteConfig()" class="btn-primary">💾 Save Site &amp; Ad Config</button>
            </div>

            <hr class="divider" />
            <h2>💰 Google AdSense &amp; Sponsor Monetization</h2>
            <p class="muted">
              Configure runtime AdSense Publisher ID (<code>ca-pub-...</code>) and slot IDs for high-traffic pages,
              or enable a sponsor banner fallback. Left empty = no empty ad boxes shown.
            </p>
            <div class="form-grid">
              <div class="field field-wide">
                <label class="chk">
                  <input type="checkbox" [(ngModel)]="adsForm.adsEnabled" />
                  <span>Enable Google AdSense Slots Across Site</span>
                </label>
              </div>
              <div class="field field-wide">
                <label>AdSense Publisher ID (Managed in Central Admin)</label>
                <div class="central-pill-row">
                  <span class="status-pill" [class.ok]="!!(adsForm.adSenseClientId || integrationsView()?.adSenseClientId)">
                    {{ adsForm.adSenseClientId || integrationsView()?.adSenseClientId || 'Not configured yet in Central Admin' }}
                  </span>
                  <a [href]="ecosystem.adminPortalUrl() + '/settings?tab=integrations'" target="_blank" rel="noopener" class="btn-sm">
                    Manage Publisher ID in Central Admin →
                  </a>
                </div>
              </div>
              <div class="field">
                <label>Ad Container Label</label>
                <input [(ngModel)]="adsForm.adLabel" placeholder="Advertisement" />
              </div>
              <div class="field"><label>Header Ad Slot ID</label><input [(ngModel)]="adsForm.headerAdSlotId" placeholder="10-digit slot ID" /></div>
              <div class="field"><label>In-Feed Ad Slot ID</label><input [(ngModel)]="adsForm.inFeedAdSlotId" placeholder="10-digit slot ID" /></div>
              <div class="field"><label>Sidebar Ad Slot ID</label><input [(ngModel)]="adsForm.sidebarAdSlotId" placeholder="10-digit slot ID" /></div>
              <div class="field"><label>Footer Ad Slot ID</label><input [(ngModel)]="adsForm.footerAdSlotId" placeholder="10-digit slot ID" /></div>

              <div class="field field-wide">
                <label class="chk">
                  <input type="checkbox" [(ngModel)]="adsForm.sponsorBannerEnabled" />
                  <span>Enable Sponsor / Affiliate Banner Fallback (when AdSense slot is unset)</span>
                </label>
              </div>
              <div class="field"><label>Sponsor Badge</label><input [(ngModel)]="adsForm.sponsorBadge" placeholder="Featured" /></div>
              <div class="field"><label>Sponsor Title</label><input [(ngModel)]="adsForm.sponsorTitle" placeholder="Creator Gear & Tools" /></div>
              <div class="field field-wide"><label>Sponsor Description</label><input [(ngModel)]="adsForm.sponsorDescription" placeholder="Short promo copy..." /></div>
              <div class="field"><label>Sponsor CTA Text</label><input [(ngModel)]="adsForm.sponsorCtaText" placeholder="Learn More" /></div>
              <div class="field"><label>Sponsor Destination URL (https://)</label><input [(ngModel)]="adsForm.sponsorUrl" placeholder="https://..." /></div>
            </div>
            <div class="btn-row">
              <button (click)="saveSiteConfig()" class="btn-primary">💾 Save AdSense &amp; Sponsor Config</button>
            </div>

            <hr class="divider" />
            <h2>🔑 Shared Third-Party Credentials &amp; Per-App OAuth</h2>
            <p class="muted">
              YouTube Data API Key, Google OAuth Client ID/Secret, and AdSense Publisher ID are stored AES-encrypted in
              <code>Central Admin (AdminDb)</code> and read server-side — never exposed over public APIs.
              Configure per-app Redirect URI and ecosystem endpoint below.
            </p>
            <div class="global-summary">
              <div>
                <strong>YouTube Data API v3 Key:</strong>
                @if (integrationsView()?.hasYouTubeApiKey) {
                  <span> ✅ Configured ({{ integrationsView()?.youTubeApiKeyMasked || '••••' }})</span>
                } @else {
                  <span> Not set — keyless RSS/public fallback active</span>
                }
              </div>
              <div>
                <strong>Google / YouTube OAuth App:</strong>
                @if (integrationsView()?.isYouTubeOAuthConfigured) {
                  <span> ✅ Active ({{ integrationsView()?.googleClientId }})</span>
                } @else {
                  <span> Not set in Central Admin</span>
                }
              </div>
              <div>
                <strong>Google AdSense Publisher ID:</strong>
                <span> {{ integrationsView()?.adSenseClientId || adsForm.adSenseClientId || 'Not set in Central Admin' }}</span>
              </div>
            </div>
            <div class="form-grid">
              <div class="field">
                <label>Central Config API URL</label>
                <input [(ngModel)]="integrationsForm.ecosystemConfigUrl" placeholder="https://id.keshavsingh.in/api/config" />
              </div>
              <div class="field">
                <label>Per-App OAuth Redirect URI</label>
                <input [(ngModel)]="integrationsForm.oAuthRedirectUri" placeholder="https://social.keshavsingh.in/youtube/callback" />
              </div>
            </div>
            <div class="btn-row">
              <button (click)="saveIntegrations()" class="btn-primary">💾 Save Per-App Routing Config</button>
              <a [href]="ecosystem.adminPortalUrl() + '/settings?tab=integrations'" target="_blank" rel="noopener" class="btn-secondary link-btn">
                Manage Shared API Keys &amp; Secrets in Central Admin →
              </a>
            </div>

            <hr class="divider" />
            <h2>Central Global Configuration (Read Live from Central Admin)</h2>
            <p class="muted">
              Global creator identity, creator channels, ecosystem navigation links, and copyright footer notice are centrally stored in
              <code>admin</code> so updating them once updates all apps together.
            </p>
            <div class="global-summary">
              <div><strong>Creator Name:</strong> {{ ecosystem.creatorName() || 'Not configured yet' }}</div>
              <div><strong>Primary YouTube Channel:</strong> {{ ecosystem.youtubeChannelName() || 'None' }} ({{ ecosystem.youtubeChannelUrl() || '—' }})</div>
              <div><strong>Synced Ecosystem Channels:</strong> {{ ecosystem.channels().length }} channel(s)</div>
              <div><strong>Footer Copyright:</strong> {{ ecosystem.copyrightNotice() || '—' }}</div>
              <div><strong>Registered Ecosystem Apps:</strong> {{ ecosystem.websites().length }} apps synced</div>
            </div>
            <div class="btn-row">
              <a [href]="ecosystem.adminPortalUrl() + '/website'" target="_blank" rel="noopener" class="btn-primary">
                Manage Channels &amp; Websites in Central Admin →
              </a>
            </div>
          </section>
        }
      }
    </div>
  `,
  styles: [`
    .admin-page { max-width: 1300px; margin: 0 auto; padding: 1.5rem 1rem 3rem; display: flex; flex-direction: column; gap: 1.5rem; }
    .denied-card { background: #fff; border: 1px solid #fecaca; border-radius: 14px; padding: 2.5rem; text-align: center; max-width: 500px; margin: 3rem auto; }
    .admin-hero { background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); color: #fff; border-radius: 14px; padding: 1.75rem; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; }
    .admin-chip { display: inline-block; background: #2563eb; color: #fff; font-size: 0.75rem; font-weight: 700; padding: 0.25rem 0.65rem; border-radius: 999px; margin-bottom: 0.5rem; }
    .admin-hero h1 { margin: 0 0 0.35rem; font-size: 1.55rem; }
    .admin-hero p { margin: 0; color: #cbd5e1; font-size: 0.9rem; }
    .hero-actions { display: flex; gap: 0.65rem; flex-wrap: wrap; }
    .btn-outline { border: 1px solid #475569; color: #f8fafc; padding: 0.55rem 0.95rem; border-radius: 8px; text-decoration: none; font-size: 0.85rem; font-weight: 600; }
    .btn-outline:hover { background: #334155; }
    .feedback-bar { background: #ecfdf5; border: 1px solid #a7f3d0; color: #065f46; padding: 0.75rem 1rem; border-radius: 8px; display: flex; justify-content: space-between; align-items: center; font-weight: 600; font-size: 0.9rem; }
    .feedback-bar.error { background: #fef2f2; border-color: #fecaca; color: #991b1b; }
    .feedback-bar button { background: transparent; border: none; cursor: pointer; font-weight: 700; }
    .tab-bar { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .tab-bar button { background: #fff; border: 1px solid #e2e8f0; color: #334155; padding: 0.6rem 1rem; border-radius: 8px; font-weight: 600; cursor: pointer; font-size: 0.9rem; }
    .tab-bar button.active { background: #2563eb; color: #fff; border-color: #2563eb; }
    .panel { background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; padding: 1.5rem; display: flex; flex-direction: column; gap: 1.25rem; }
    .panel h2 { margin: 0; font-size: 1.15rem; color: #0f172a; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 0.9rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; }
    .field-wide { grid-column: 1 / -1; }
    .field label { font-size: 0.78rem; font-weight: 700; color: #475569; text-transform: uppercase; letter-spacing: 0.02em; }
    .field label.chk, label.chk { display: inline-flex; flex-direction: row; align-items: center; gap: 0.55rem; font-size: 0.9rem; font-weight: 600; color: #1e293b; text-transform: none; letter-spacing: normal; cursor: pointer; }
    .field label.chk input[type="checkbox"], label.chk input[type="checkbox"] { width: auto; margin: 0; accent-color: #2563eb; cursor: pointer; }
    .central-pill-row { display: flex; align-items: center; flex-wrap: wrap; gap: 0.65rem; }
    .status-pill { display: inline-flex; align-items: center; padding: 0.35rem 0.75rem; border-radius: 999px; font-size: 0.82rem; font-weight: 600; background: #f1f5f9; color: #475569; border: 1px solid #cbd5e1; }
    .status-pill.ok { background: #ecfdf5; color: #047857; border-color: #a7f3d0; }
    .link-btn { text-decoration: none; display: inline-flex; align-items: center; }
    .field input, .field select, .field textarea { border: 1px solid #cbd5e1; border-radius: 7px; padding: 0.5rem 0.65rem; font-size: 0.9rem; color: #0f172a; font-family: inherit; }
    .checkbox-field { justify-content: flex-end; }
    .checkbox-field label { text-transform: none; font-size: 0.9rem; display: flex; align-items: center; gap: 0.45rem; cursor: pointer; }
    .btn-row { display: flex; gap: 0.75rem; flex-wrap: wrap; }
    .btn-primary { background: #2563eb; color: #fff; border: none; padding: 0.6rem 1.2rem; border-radius: 8px; font-weight: 600; cursor: pointer; text-decoration: none; font-size: 0.9rem; }
    .btn-primary:hover { background: #1d4ed8; }
    .btn-secondary { background: #f1f5f9; color: #334155; border: 1px solid #cbd5e1; padding: 0.6rem 1rem; border-radius: 8px; font-weight: 600; cursor: pointer; }
    .table-wrap { overflow-x: auto; }
    .admin-table { width: 100%; border-collapse: collapse; font-size: 0.88rem; }
    .admin-table th, .admin-table td { text-align: left; padding: 0.65rem 0.75rem; border-bottom: 1px solid #e2e8f0; }
    .admin-table th { background: #f8fafc; font-weight: 700; color: #475569; font-size: 0.78rem; text-transform: uppercase; }
    .act-cell { display: flex; gap: 0.4rem; flex-wrap: wrap; }
    .btn-sm { background: #eff6ff; color: #1d4ed8; border: 1px solid #bfdbfe; padding: 0.3rem 0.6rem; border-radius: 6px; font-size: 0.8rem; font-weight: 600; cursor: pointer; }
    .btn-sm.danger { background: #fef2f2; color: #dc2626; border-color: #fecaca; }
    .pill { background: #ecfdf5; color: #047857; padding: 0.15rem 0.5rem; border-radius: 999px; font-size: 0.75rem; font-weight: 700; }
    .pill.inactive { background: #f1f5f9; color: #64748b; }
    .divider { border: none; border-top: 1px solid #e2e8f0; margin: 0.5rem 0; }
    .stack { display: flex; flex-direction: column; gap: 0.85rem; }
    .subcard { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1rem; display: flex; flex-direction: column; gap: 0.75rem; }
    .row-between { display: flex; justify-content: space-between; align-items: center; }
    .muted { color: #64748b; font-size: 0.9rem; margin: 0; }
    .global-summary { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 1rem; display: flex; flex-direction: column; gap: 0.4rem; font-size: 0.9rem; color: #1e293b; }
  `]
})
export class SocialAdminComponent implements OnInit {
  readonly auth = inject(AuthService);
  private analytics = inject(AnalyticsService);
  readonly ecosystem = inject(EcosystemConfigService);

  readonly activeTab = signal<AdminTab>('channels');
  readonly status = signal<AdminStatus | null>(null);
  readonly feedback = signal<{ message: string; isError: boolean } | null>(null);
  readonly syncing = signal(false);
  readonly integrationsView = signal<IntegrationSettingsView | null>(null);

  readonly channels = signal<ChannelMetrics[]>([]);
  readonly funnels = signal<FunnelCampaign[]>([]);
  readonly suggestions = signal<GrowthSuggestion[]>([]);

  integrationsForm = {
    ecosystemConfigUrl: 'https://id.keshavsingh.in/api/config',
    oAuthRedirectUri: ''
  };

  channelForm: ChannelMetrics = this.emptyChannel();
  funnelForm: FunnelCampaign = this.emptyFunnel();
  suggestionForm: GrowthSuggestion = this.emptySuggestion();
  growthConfigForm: GrowthConfig = { bestUploadTimes: {}, highDemandKeywords: [], targetEngagementPercent: 6.5, targetAvgWatchMinutes: 5 };
  uploadTimeEntries: Array<{ platform: string; window: string }> = [];
  keywordsCsv = '';

  earningsForm: EarningsConfig = { platformBenchmarks: [], niches: [], opportunities: [] };
  adsForm: AdMonetizationConfig = this.emptyAds();
  siteForm: SiteConfig = {
    heroTitle: '',
    heroSubtitle: '',
    youtubeSnippetHeader: '',
    youtubeSnippetFooter: '',
    hashtags: '',
    defaultDestinationBaseUrl: '',
    adminPortalUrl: 'https://admin.keshavsingh.in'
  };

  ngOnInit(): void {
    this.ecosystem.load();
    if (this.auth.isAdmin()) {
      this.loadAll();
    }
  }

  private loadAll(): void {
    this.analytics.getAdminStatus().subscribe({
      next: s => this.status.set(s),
      error: err => this.notifyError('Failed to verify Admin API session', err)
    });
    this.analytics.getAdminChannels().subscribe({
      next: list => this.channels.set(list),
      error: err => this.notifyError('Failed to load channels', err)
    });
    this.analytics.getAdminFunnels().subscribe({
      next: list => this.funnels.set(list),
      error: err => this.notifyError('Failed to load funnels', err)
    });
    this.analytics.getAdminSuggestions().subscribe({
      next: list => this.suggestions.set(list),
      error: err => this.notifyError('Failed to load recommendations', err)
    });
    this.analytics.getIntegrations().subscribe({
      next: v => {
        this.integrationsView.set(v);
        this.integrationsForm.ecosystemConfigUrl = v.ecosystemConfigUrl || 'https://id.keshavsingh.in/api/config';
        this.integrationsForm.oAuthRedirectUri = v.oAuthRedirectUri || '';
      },
      error: err => this.notifyError('Failed to load integrations status', err)
    });
    this.analytics.getGrowthConfig().subscribe(cfg => {
      this.growthConfigForm = cfg;
      this.keywordsCsv = (cfg.highDemandKeywords || []).join(', ');
      this.uploadTimeEntries = Object.entries(cfg.bestUploadTimes || {}).map(([platform, window]) => ({ platform, window }));
    });
    this.analytics.getEarningsConfig().subscribe(cfg => {
      this.earningsForm = {
        ...cfg,
        platformBenchmarks: cfg.platformBenchmarks.map(x => ({ ...x })),
        niches: cfg.niches.map(x => ({ ...x })),
        opportunities: cfg.opportunities.map(x => ({ ...x }))
      };
    });
    this.analytics.getSiteConfig().subscribe(cfg => {
      this.siteForm = { ...cfg };
      this.adsForm = { ...this.emptyAds(), ...(cfg.ads || {}) };
    });
  }

  // ---- Central Sync & Integrations ----
  runSyncEcosystem(purgeLegacy: boolean): void {
    this.syncing.set(true);
    this.analytics.syncEcosystem(purgeLegacy).subscribe({
      next: res => {
        this.syncing.set(false);
        this.notify(res.message);
        this.loadAll();
      },
      error: err => {
        this.syncing.set(false);
        this.notifyError('Failed to sync ecosystem', err);
      }
    });
  }

  runRefreshYouTube(): void {
    this.syncing.set(true);
    this.analytics.syncAllYouTube().subscribe({
      next: res => {
        this.syncing.set(false);
        this.notify(res.message);
        this.loadAll();
      },
      error: err => {
        this.syncing.set(false);
        this.notifyError('Failed to refresh YouTube feeds', err);
      }
    });
  }

  syncSingleChannel(id: string): void {
    this.analytics.syncChannelLive(id).subscribe({
      next: updated => {
        this.channels.update(list => list.map(c => c.id === updated.id ? updated : c));
        this.notify(`Synced live data for ${updated.channelName} (${updated.recentVideos?.length || 0} recent videos).`);
      },
      error: err => this.notifyError('Live sync failed', err)
    });
  }

  runPurgeLegacy(): void {
    this.analytics.purgeLegacyDemo().subscribe({
      next: res => {
        this.notify(`Removed ${res.purgedChannels} legacy demo channel(s).`);
        this.analytics.getAdminChannels().subscribe(list => this.channels.set(list));
      },
      error: err => this.notifyError('Purge failed', err)
    });
  }

  saveIntegrations(): void {
    this.analytics.saveIntegrations({
      oAuthRedirectUri: this.integrationsForm.oAuthRedirectUri,
      ecosystemConfigUrl: this.integrationsForm.ecosystemConfigUrl
    }).subscribe({
      next: saved => {
        this.integrationsView.set(saved);
        this.integrationsForm.oAuthRedirectUri = saved.oAuthRedirectUri || '';
        this.notify('Per-app routing settings saved.');
      },
      error: err => this.notifyError('Failed to save integration settings', err)
    });
  }

  // ---- Channels ----
  editChannel(ch: ChannelMetrics): void { this.channelForm = { ...ch }; }
  resetChannelForm(): void { this.channelForm = this.emptyChannel(); }
  saveChannel(): void {
    this.analytics.saveChannel(this.channelForm).subscribe({
      next: () => {
        this.notify('Channel saved to database.');
        this.resetChannelForm();
        this.analytics.getAdminChannels().subscribe(list => this.channels.set(list));
      },
      error: err => this.notifyError('Failed to save channel', err)
    });
  }
  removeChannel(id: string): void {
    this.analytics.deleteChannel(id).subscribe({
      next: () => {
        this.channels.update(list => list.filter(c => c.id !== id));
        this.notify('Channel removed.');
      },
      error: err => this.notifyError('Failed to delete channel', err)
    });
  }

  // ---- Funnels ----
  editFunnel(f: FunnelCampaign): void { this.funnelForm = { ...f }; }
  resetFunnelForm(): void { this.funnelForm = this.emptyFunnel(); }
  saveFunnel(): void {
    this.analytics.saveFunnel(this.funnelForm).subscribe({
      next: () => {
        this.notify('Traffic funnel saved.');
        this.resetFunnelForm();
        this.analytics.getAdminFunnels().subscribe(list => this.funnels.set(list));
      },
      error: err => this.notifyError('Failed to save funnel', err)
    });
  }
  addConversion(id: string): void {
    this.analytics.recordConversion(id).subscribe({
      next: updated => {
        this.funnels.update(list => list.map(f => f.id === updated.id ? updated : f));
        this.notify(`Recorded conversion on "${updated.title}"`);
      },
      error: err => this.notifyError('Failed to update conversions', err)
    });
  }
  removeFunnel(id: string): void {
    this.analytics.deleteFunnel(id).subscribe({
      next: () => {
        this.funnels.update(list => list.filter(f => f.id !== id));
        this.notify('Funnel removed.');
      },
      error: err => this.notifyError('Failed to delete funnel', err)
    });
  }

  // ---- Growth Advisor ----
  editSuggestion(s: GrowthSuggestion): void { this.suggestionForm = { ...s }; }
  resetSuggestionForm(): void { this.suggestionForm = this.emptySuggestion(); }
  saveSuggestion(): void {
    this.analytics.saveSuggestion(this.suggestionForm).subscribe({
      next: () => {
        this.notify('Recommendation rule saved.');
        this.resetSuggestionForm();
        this.analytics.getAdminSuggestions().subscribe(list => this.suggestions.set(list));
      },
      error: err => this.notifyError('Failed to save recommendation rule', err)
    });
  }
  removeSuggestion(id: string): void {
    this.analytics.deleteSuggestion(id).subscribe({
      next: () => {
        this.suggestions.update(list => list.filter(s => s.id !== id));
        this.notify('Rule removed.');
      },
      error: err => this.notifyError('Failed to delete rule', err)
    });
  }
  saveGrowthConfig(): void {
    const times: Record<string, string> = {};
    for (const e of this.uploadTimeEntries) {
      if (e.platform.trim()) times[e.platform.trim()] = e.window;
    }
    const payload: GrowthConfig = {
      ...this.growthConfigForm,
      bestUploadTimes: times,
      highDemandKeywords: this.keywordsCsv.split(',').map(k => k.trim()).filter(Boolean)
    };
    this.analytics.saveGrowthConfig(payload).subscribe({
      next: saved => {
        this.growthConfigForm = saved;
        this.notify('Growth targets, upload windows & keywords updated.');
      },
      error: err => this.notifyError('Failed to save growth config', err)
    });
  }

  // ---- Earnings Config ----
  addNiche(): void {
    this.earningsForm.niches.push({
      id: 'n-' + Date.now(),
      name: 'New Niche',
      minRpmUsd: 1.5,
      maxRpmUsd: 5.0,
      suggestedRpmUsd: 3.0,
      tip: ''
    });
  }
  removeNiche(index: number): void { this.earningsForm.niches.splice(index, 1); }
  addOpportunity(): void {
    this.earningsForm.opportunities.push({
      id: 'opp-' + Date.now(),
      title: 'New Opportunity',
      platform: 'All',
      impact: 'High',
      description: '',
      actionUrl: this.ecosystem.websites()[0]?.url || ''
    });
  }
  removeOpportunity(index: number): void { this.earningsForm.opportunities.splice(index, 1); }
  saveEarningsConfig(): void {
    this.analytics.saveEarningsConfig(this.earningsForm).subscribe({
      next: saved => {
        this.earningsForm = saved;
        this.notify('Earnings benchmarks, niches & opportunities saved.');
      },
      error: err => this.notifyError('Failed to save earnings config', err)
    });
  }

  // ---- Site Config ----
  saveSiteConfig(): void {
    this.analytics.saveSiteConfig({ ...this.siteForm, ads: { ...this.adsForm } }).subscribe({
      next: saved => {
        this.siteForm = { ...saved };
        this.adsForm = { ...this.emptyAds(), ...(saved.ads || {}) };
        this.notify('SocialPulse site & monetization configuration saved.');
      },
      error: err => this.notifyError('Failed to save site config', err)
    });
  }

  private emptyAds(): AdMonetizationConfig {
    return {
      adsEnabled: false,
      adSenseClientId: '',
      headerAdSlotId: '',
      inFeedAdSlotId: '',
      sidebarAdSlotId: '',
      footerAdSlotId: '',
      adLabel: 'Advertisement',
      sponsorBannerEnabled: false,
      sponsorBadge: 'Featured',
      sponsorTitle: '',
      sponsorDescription: '',
      sponsorCtaText: 'Learn More',
      sponsorUrl: ''
    };
  }

  private notify(message: string): void {
    this.feedback.set({ message, isError: false });
  }
  private notifyError(prefix: string, err: { status?: number }): void {
    const code = err?.status ? ` (HTTP ${err.status})` : '';
    this.feedback.set({ message: `${prefix}${code}`, isError: true });
  }

  private emptyChannel(): ChannelMetrics {
    return {
      id: '',
      channelName: '',
      channelHandle: '',
      platform: 'YouTube' as SocialPlatform,
      niche: 'AI & Dev',
      followersOrSubscribers: 0,
      totalViewsLast30Days: 0,
      engagementRatePercentage: 0,
      averageViewDurationMinutes: 0,
      videosOrPostsCount: 0,
      growthRateMonthlyPercentage: 0,
      rpmUsd: 3.2,
      estimatedMonthlyRevenue: 0,
      channelUrl: '',
      isActive: true,
      lastUpdated: new Date().toISOString()
    };
  }

  private emptyFunnel(): FunnelCampaign {
    return {
      id: '',
      title: '',
      sourcePlatform: 'YouTube Description',
      destinationUrl: this.ecosystem.websites()[0]?.url || '',
      trackingSlug: '',
      totalClicks: 0,
      conversions: 0,
      conversionRatePercentage: 0,
      isActive: true
    };
  }

  private emptySuggestion(): GrowthSuggestion {
    return {
      id: '',
      category: 'Retention & Drop-off Fix',
      title: '',
      description: '',
      impact: 'High',
      actionItem: '',
      estimatedGain: '+15% Retention',
      applicablePlatform: 'YouTube'
    };
  }
}
