import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AnalyticsService } from '../../core/services/analytics.service';
import { EcosystemConfigService } from '../../core/services/ecosystem-config.service';
import { AuthService } from '../../core/services/auth.service';
import { AdSlotComponent } from '../shared/ad-slot/ad-slot.component';
import { ChannelAnalyzerComponent } from '../shared/channel-analyzer/channel-analyzer.component';
import { LoadingStateComponent } from '../shared/loading/loading-state.component';
import {
  ChannelMetrics,
  PlatformSummary,
  RecentFeedVideo,
  SiteConfig,
  YouTubeConnectionView
} from '../../core/models/analytics.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, AdSlotComponent, ChannelAnalyzerComponent, LoadingStateComponent],
  template: `
    <div class="dashboard-page">
      @if (adminDenied()) {
        <div class="alert-banner">
          <span>🛡️ <strong>Admin Access Required:</strong> Only users with the <code>Admin</code> role can open the Analytics Admin Console.</span>
          <button type="button" (click)="dismissDenied()" class="alert-close">✕</button>
        </div>
      }

      @if (analytics.backendOffline()) {
        <div class="alert-banner warn">
          <span>⚠️ Backend analytics service is waking up or unreachable right now. Live metrics will appear once connected.</span>
        </div>
      }

      @if (statusToast()) {
        <div class="alert-banner info">
          <span>{{ statusToast() }}</span>
          <button type="button" (click)="statusToast.set('')" class="alert-close">✕</button>
        </div>
      }

      <!-- Hero Command Banner -->
      <section class="hero-banner">
        <div class="hero-content">
          <div class="hero-badge">
            <span class="pulse-dot"></span>
            @if (auth.isAuthenticated()) {
              <span>Private Workspace • {{ verifiedCount() }} / {{ channels().length }} Connected</span>
            } @else {
              <span>Instant Live Channel Analyzer • Public Creator Intelligence</span>
            }
          </div>
          <h1>{{ siteConfig()?.heroTitle || 'Social Channel Performance & Growth Hub' }}</h1>
          <p>
            {{ siteConfig()?.heroSubtitle || 'Search any YouTube or social handle for instant analytics, RPM forecasts, and growth insights — or sign in to connect your own private channels.' }}
          </p>
          <div class="hero-cta-group">
            <a routerLink="/earnings" class="btn btn-primary">💰 Calculate Earnings</a>
            <a routerLink="/advisor" class="btn btn-secondary">🚀 Growth Advisor</a>
            <a routerLink="/funnel" class="btn btn-outline">🎯 Traffic Funnels</a>
            @if (oauthConfigured() && auth.isAuthenticated()) {
              <button type="button" (click)="connectYouTube()" [disabled]="oauthConnecting()" class="btn btn-yt">
                ▶ {{ oauthConnecting() ? 'Opening Google...' : 'Connect YouTube' }}
              </button>
            } @else if (!auth.isAuthenticated()) {
              <button type="button" (click)="auth.loginRedirect()" class="btn btn-outline">🔐 Sign in to Connect Channels</button>
            }
            @if (auth.isAdmin()) {
              <a routerLink="/admin" class="btn btn-admin">🛡️ Admin Console</a>
            }
          </div>
        </div>

        <div class="hero-kpi-grid">
          <div class="stat-pill">
            <span class="stat-num">{{ totalViewsLabel() }}</span>
            <span class="stat-label">{{ auth.isAuthenticated() && channels().length > 0 ? 'My Tracked Views (30d)' : 'Analyzed Channel Views' }}</span>
          </div>
          <div class="stat-pill">
            <span class="stat-num">{{ totalFollowersLabel() }}</span>
            <span class="stat-label">{{ auth.isAuthenticated() && channels().length > 0 ? 'My Total Audience' : 'Analyzed Subscribers' }}</span>
          </div>
          <div class="stat-pill">
            <span class="stat-num">{{ loading() ? '…' : activeVideoCount() }}</span>
            <span class="stat-label">Recent Videos Loaded</span>
          </div>
          <div class="stat-pill accent">
            <span class="stat-num">{{ loading() ? '…' : ('$' + (totalRevenue() | number:'1.0-0')) }}</span>
            <span class="stat-label">Est. Monthly RPM Rev</span>
          </div>
        </div>
      </section>

      <app-ad-slot placement="header" />

      <app-channel-analyzer (channelSaved)="onChannelSaved($event)" />

      <!-- Signed-in User Connected Platforms & YouTube OAuth -->
      @if (auth.isAuthenticated()) {
        <section class="workspace-bar">
          <div class="workspace-info">
            <h3>👤 My Private Channel Workspace</h3>
            <p>Only channels you connect or save are shown here. Other visitors cannot view your analytics.</p>
          </div>
          <div class="workspace-controls">
            @if (oauthConfigured()) {
              <button type="button" (click)="connectYouTube()" [disabled]="oauthConnecting()" class="btn btn-yt">
                ▶ {{ oauthConnecting() ? 'Connecting...' : 'Connect YouTube via OAuth' }}
              </button>
            } @else {
              <span class="oauth-hint">Use Instant Search above &amp; click "+ Save to My Channels" to add any &#64;handle automatically.</span>
            }
          </div>
        </section>

        @if (youtubeConnections().length > 0) {
          <section class="connections-strip">
            @for (conn of youtubeConnections(); track conn.channelId) {
              <div class="conn-pill">
                @if (conn.avatarUrl) {
                  <img [src]="conn.avatarUrl" [alt]="conn.channelTitle" />
                }
                <div class="conn-meta">
                  <strong>{{ conn.channelTitle }}</strong>
                  <span>{{ conn.channelHandle || conn.channelId }} • {{ conn.needsReconnect ? 'Reconnect needed' : 'OAuth linked' }}</span>
                </div>
                <button type="button" class="btn-chip" (click)="syncOAuthConnection(conn.channelId)">Sync</button>
                <button type="button" class="btn-chip danger" (click)="disconnectOAuth(conn.channelId)">Disconnect</button>
              </div>
            }
          </section>
        }
      } @else {
        <section class="empty-state-card">
          <div class="empty-icon">🔐</div>
          <h2>Your Personal Analytics Workspace Is Private</h2>
          <p>
            Search and analyze any creator &#64;handle above right away — or sign in with SSO to connect your YouTube account,
            track your own channels privately, and create trackable monetization funnels.
          </p>
          <div class="empty-actions">
            <button type="button" (click)="auth.loginRedirect()" class="btn btn-primary">Sign in with SSO</button>
          </div>
        </section>
      }

      @if (auth.isAuthenticated() && loading() && channels().length === 0) {
        <app-loading-state variant="cards" [count]="3" label="Loading workspace channels & live analytics…" />
      }

      @if (auth.isAuthenticated() && !loading() && channels().length === 0) {
        <section class="empty-state-card">
          <div class="empty-icon">📡</div>
          <h2>No Saved Channels in Your Workspace Yet</h2>
          <p>
            Search your YouTube &#64;handle or channel name above and click <strong>+ Save to My Channels</strong>
            @if (oauthConfigured()) {
              <span>, or click <strong>Connect YouTube via OAuth</strong> above</span>
            }
            to start tracking your private metrics.
          </p>
        </section>
      }

      <!-- Platform Overview Cards + Distribution Bars -->
      @if (platformSummaries().length > 0) {
        <section class="section">
          <div class="section-header">
            <div>
              <h2>Platform Distribution &amp; RPM Value</h2>
              <span class="section-sub">Aggregated privately from your connected workspace channels</span>
            </div>
            <span class="section-tag">{{ platformSummaries().length }} Active Platform(s)</span>
          </div>

          <div class="grid grid-3">
            @for (summary of platformSummaries(); track summary.platform) {
              <div class="platform-card" [attr.data-platform]="summary.platform.toLowerCase()">
                <div class="plat-head">
                  <div class="plat-title-wrap">
                    <span class="plat-badge">{{ getPlatformIcon(summary.platform) }} {{ summary.platform }}</span>
                    @if (summary.channelCount) {
                      <small class="plat-count">{{ summary.channelCount }} channel(s)</small>
                    }
                  </div>
                  <span class="plat-rev">\${{ summary.estimatedEarningsMonthly | number:'1.0-2' }}/mo</span>
                </div>
                <div class="plat-body">
                  <div class="metric">
                    <span class="m-val">{{ summary.totalAudience | number }}</span>
                    <span class="m-lbl">Audience / Subs</span>
                  </div>
                  <div class="metric">
                    <span class="m-val">{{ summary.totalViews | number }}</span>
                    <span class="m-lbl">Tracked Views</span>
                  </div>
                  <div class="metric">
                    <span class="m-val">{{ summary.avgEngagement }}%</span>
                    <span class="m-lbl">Avg Engagement</span>
                  </div>
                </div>
                <div class="share-bar-wrap">
                  <div class="share-labels">
                    <span>Traffic Share</span>
                    <strong>{{ platformViewShare(summary.totalViews) }}%</strong>
                  </div>
                  <div class="share-track">
                    <div class="share-fill" [style.width.%]="platformViewShare(summary.totalViews)"></div>
                  </div>
                </div>
              </div>
            }
          </div>
        </section>
      }

      <!-- Connected Channels Performance Index -->
      @if (channels().length > 0) {
        <section class="section">
          <div class="section-header">
            <div>
              <h2>My Connected Channels &amp; Live Index</h2>
              <span class="section-sub">Your private synced channels and connected social accounts</span>
            </div>
            <div class="filter-pills">
              <button type="button" [class.active]="selectedPlatform() === 'ALL'" (click)="selectedPlatform.set('ALL')">
                All ({{ channels().length }})
              </button>
              @for (plat of availablePlatforms(); track plat) {
                <button type="button" [class.active]="selectedPlatform() === plat" (click)="selectedPlatform.set(plat)">
                  {{ getPlatformIcon(plat) }} {{ plat }}
                </button>
              }
            </div>
          </div>

          <div class="table-container">
            <table class="data-table">
              <thead>
                <tr>
                  <th>Channel / Account</th>
                  <th>Source</th>
                  <th>Niche</th>
                  <th>Subscribers</th>
                  <th>Recent / 30d Views</th>
                  <th>Latest Videos</th>
                  <th>RPM / Est. Rev</th>
                  <th>Synced / Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (channel of filteredChannels(); track channel.id) {
                  <tr>
                    <td>
                      <div class="channel-cell">
                        @if (channel.avatarUrl) {
                          <img [src]="channel.avatarUrl" [alt]="channel.channelName" class="ch-avatar" />
                        } @else {
                          <div class="ch-avatar-fallback">{{ getPlatformIcon(channel.platform) }}</div>
                        }
                        <div class="channel-identity">
                          @if (channel.channelUrl) {
                            <a [href]="channel.channelUrl" target="_blank" rel="noopener" class="ch-link">
                              {{ channel.channelName }} ↗
                            </a>
                          } @else {
                            <strong>{{ channel.channelName }}</strong>
                          }
                          <span class="channel-handle">{{ channel.channelHandle }} • {{ channel.platform }}</span>
                        </div>
                      </div>
                    </td>
                    <td>
                      <span class="source-pill" [class.live]="channel.isVerifiedLive">
                        {{ channel.isVerifiedLive ? '● ' : '' }}{{ channel.dataSource || 'Admin' }}
                      </span>
                    </td>
                    <td><span class="niche-chip">{{ channel.niche }}</span></td>
                    <td><strong>{{ channel.followersOrSubscribers | number }}</strong></td>
                    <td><strong>{{ channel.totalViewsLast30Days | number }}</strong></td>
                    <td>{{ channel.recentVideos?.length || channel.videosOrPostsCount }}</td>
                    <td>
                      <span class="rev-pill">\${{ channel.estimatedMonthlyRevenue || 0 | number:'1.0-2' }}</span>
                      <small class="rpm-sub">\${{ channel.rpmUsd || 0 }} RPM</small>
                    </td>
                    <td class="sync-date">
                      <div>{{ channel.lastSyncedAt ? (channel.lastSyncedAt | date:'MMM d, HH:mm') : (channel.lastUpdated | date:'MMM d') }}</div>
                      @if (!channel.isAdminOwned && auth.isAuthenticated()) {
                        <div class="row-actions">
                          <button type="button" class="btn-chip" (click)="refreshUserChannel(channel.id)">Refresh</button>
                          <button type="button" class="btn-chip danger" (click)="removeUserChannel(channel.id)">Remove</button>
                        </div>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      }

      <app-ad-slot placement="infeed" />

      <!-- Live Latest Videos Feed -->
      @if (recentVideos().length > 0) {
        <section class="section">
          <div class="section-header">
            <div>
              <h2>📺 Latest Live Videos &amp; Engagement</h2>
              <span class="section-sub">Real videos pulled directly from your connected YouTube channel feeds</span>
            </div>
            <span class="section-tag">{{ recentVideos().length }} Recent Uploads</span>
          </div>

          <div class="videos-grid">
            @for (vid of recentVideos().slice(0, 8); track vid.videoId) {
              <a [href]="vid.url" target="_blank" rel="noopener" class="video-card">
                <div class="thumb-wrap">
                  <img [src]="vid.thumbnailUrl" [alt]="vid.title" loading="lazy" decoding="async" />
                  <span class="views-badge">👁 {{ vid.views | number }}</span>
                </div>
                <div class="video-body">
                  <span class="vid-channel">{{ vid.channelName }} • {{ vid.channelHandle }}</span>
                  <h4>{{ vid.title }}</h4>
                  <div class="vid-meta">
                    <span>{{ vid.publishedAt | date:'mediumDate' }}</span>
                    <span class="watch-cta">Watch on YouTube →</span>
                  </div>
                </div>
              </a>
            }
          </div>
        </section>
      }

      <!-- Sibling Cross-Promotion & Traffic Generation Blocks (Synced from Admin Ecosystem Registry) -->
      @if (promoSites().length > 0) {
        <section class="section">
          <div class="section-header">
            <div>
              <h2>🌐 Ecosystem Traffic Destinations</h2>
              <span class="section-sub">Route channel traffic directly into connected web portals</span>
            </div>
            <a routerLink="/funnel" class="manage-link">Build Funnel Links →</a>
          </div>
          <div class="cross-promo-grid">
            @for (site of promoSites(); track site.key) {
              <div class="promo-card"
                   [class.studio-card]="site.key === 'studio'"
                   [class.panchang-card]="site.key === 'panchang'"
                   [class.sibling-card]="site.key !== 'studio' && site.key !== 'panchang'">
                <div class="promo-tag">Ecosystem App</div>
                <h3>{{ site.icon || '🌐' }} {{ site.name }}</h3>
                <p>{{ site.description || 'Open this connected web platform.' }}</p>
                <a [href]="site.url" target="_blank" rel="noopener" class="btn btn-accent">Open {{ site.name }} →</a>
              </div>
            }
          </div>
        </section>
      }
    </div>
  `,
  styles: [`
    .dashboard-page {
      max-width: 1320px;
      margin: 0 auto;
      padding: 1.5rem 1rem 3rem;
      display: flex;
      flex-direction: column;
      gap: 2rem;
    }
    .hero-banner {
      background: radial-gradient(circle at top right, #1e3a8a 0%, #0f172a 55%, #020617 100%);
      color: #ffffff;
      border-radius: 18px;
      padding: 2.25rem;
      display: grid;
      grid-template-columns: 1.35fr 1fr;
      align-items: center;
      gap: 2rem;
      box-shadow: 0 16px 35px -10px rgba(15, 23, 42, 0.45);
    }
    @media (max-width: 960px) {
      .hero-banner { grid-template-columns: 1fr; padding: 1.65rem; }
    }
    .hero-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
      background: rgba(56, 189, 248, 0.14);
      border: 1px solid rgba(56, 189, 248, 0.35);
      color: #bae6fd;
      padding: 0.3rem 0.75rem;
      border-radius: 999px;
      font-size: 0.78rem;
      font-weight: 600;
      margin-bottom: 0.85rem;
    }
    .pulse-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #22c55e;
      box-shadow: 0 0 8px #22c55e;
    }
    .hero-content h1 {
      font-size: 2.05rem;
      font-weight: 800;
      line-height: 1.18;
      margin: 0 0 0.75rem;
      letter-spacing: -0.02em;
    }
    .hero-content p {
      color: #cbd5e1;
      font-size: 0.98rem;
      line-height: 1.6;
      margin: 0 0 1.4rem;
      max-width: 620px;
    }
    .hero-cta-group {
      display: flex;
      gap: 0.7rem;
      flex-wrap: wrap;
    }
    .hero-kpi-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 0.85rem;
    }
    .stat-pill {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      background: rgba(255, 255, 255, 0.06);
      backdrop-filter: blur(8px);
      border: 1px solid rgba(255, 255, 255, 0.12);
      padding: 1rem 1.15rem;
      border-radius: 14px;
    }
    .stat-pill.accent {
      background: rgba(16, 185, 129, 0.12);
      border-color: rgba(16, 185, 129, 0.35);
    }
    .stat-num {
      font-size: 1.55rem;
      font-weight: 800;
      color: #38bdf8;
      letter-spacing: -0.02em;
    }
    .stat-pill.accent .stat-num { color: #34d399; }
    .stat-label {
      font-size: 0.72rem;
      color: #cbd5e1;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      font-weight: 600;
    }

    .btn {
      padding: 0.62rem 1.15rem;
      border-radius: 9px;
      font-weight: 600;
      text-decoration: none;
      display: inline-block;
      font-size: 0.9rem;
      cursor: pointer;
      border: none;
      transition: all 0.2s ease;
    }
    .btn-primary { background: #2563eb; color: #ffffff; }
    .btn-primary:hover { background: #1d4ed8; }
    .btn-secondary { background: #059669; color: #ffffff; }
    .btn-secondary:hover { background: #047857; }
    .btn-outline { background: rgba(255,255,255,0.06); color: #ffffff; border: 1px solid rgba(255,255,255,0.22); }
    .btn-outline:hover { background: rgba(255,255,255,0.14); }
    .btn-admin { background: #f59e0b; color: #0f172a; font-weight: 700; }
    .btn-admin:hover { background: #d97706; color: #ffffff; }
    .btn-accent { background: #ffffff; color: #0f172a; font-weight: 700; align-self: flex-start; }
    .btn-accent:hover { background: #f1f5f9; }

    .empty-state-card {
      background: #ffffff;
      border: 2px dashed #cbd5e1;
      border-radius: 16px;
      padding: 2.5rem 1.5rem;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
    }
    .empty-icon { font-size: 2.5rem; }
    .empty-state-card h2 { margin: 0; color: #0f172a; font-size: 1.35rem; }
    .empty-state-card p { margin: 0; color: #475569; max-width: 580px; line-height: 1.55; }

    .section {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .section-header {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .section-header h2 {
      font-size: 1.35rem;
      font-weight: 800;
      color: #0f172a;
      margin: 0;
    }
    .section-sub {
      font-size: 0.85rem;
      color: #64748b;
    }
    .section-tag {
      font-size: 0.78rem;
      background: #eff6ff;
      color: #1d4ed8;
      padding: 0.25rem 0.7rem;
      border-radius: 9999px;
      font-weight: 700;
    }
    .manage-link {
      color: #2563eb;
      font-weight: 600;
      font-size: 0.88rem;
      text-decoration: none;
    }
    .filter-pills {
      display: flex;
      gap: 0.4rem;
      flex-wrap: wrap;
    }
    .filter-pills button {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      color: #475569;
      padding: 0.35rem 0.75rem;
      border-radius: 999px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
    }
    .filter-pills button.active {
      background: #0f172a;
      color: #ffffff;
      border-color: #0f172a;
    }

    .grid { display: grid; gap: 1.25rem; }
    .grid-3 { grid-template-columns: repeat(auto-fit, minmax(290px, 1fr)); }

    .platform-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      padding: 1.25rem;
      box-shadow: 0 2px 6px rgba(15, 23, 42, 0.04);
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .plat-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-bottom: 0.75rem;
      border-bottom: 1px solid #f1f5f9;
    }
    .plat-title-wrap { display: flex; flex-direction: column; gap: 0.15rem; }
    .plat-badge { font-weight: 800; font-size: 1.05rem; color: #0f172a; }
    .plat-count { font-size: 0.75rem; color: #64748b; }
    .plat-rev { font-weight: 800; color: #059669; font-size: 1.05rem; }
    .plat-body {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 0.5rem;
      text-align: center;
    }
    .metric .m-val { display: block; font-size: 1.15rem; font-weight: 800; color: #0f172a; }
    .metric .m-lbl { font-size: 0.72rem; color: #64748b; text-transform: uppercase; }
    .share-bar-wrap { display: flex; flex-direction: column; gap: 0.3rem; }
    .share-labels { display: flex; justify-content: space-between; font-size: 0.76rem; color: #475569; }
    .share-track { height: 7px; background: #e2e8f0; border-radius: 999px; overflow: hidden; }
    .share-fill { height: 100%; background: linear-gradient(90deg, #2563eb, #06b6d4); border-radius: 999px; }

    .table-container {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      overflow-x: auto;
      box-shadow: 0 2px 6px rgba(15, 23, 42, 0.03);
    }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.9rem; }
    .data-table th {
      background: #f8fafc;
      color: #475569;
      font-weight: 700;
      font-size: 0.76rem;
      text-transform: uppercase;
      letter-spacing: 0.03em;
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #e2e8f0;
    }
    .data-table td { padding: 0.9rem 1rem; border-bottom: 1px solid #f1f5f9; color: #1e293b; vertical-align: middle; }
    .channel-cell { display: flex; align-items: center; gap: 0.75rem; }
    .ch-avatar, .ch-avatar-fallback {
      width: 38px;
      height: 38px;
      border-radius: 50%;
      object-fit: cover;
      flex-shrink: 0;
      background: #e2e8f0;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.1rem;
    }
    .channel-identity { display: flex; flex-direction: column; gap: 0.1rem; }
    .ch-link { color: #0f172a; font-weight: 700; text-decoration: none; }
    .ch-link:hover { color: #2563eb; text-decoration: underline; }
    .channel-handle { font-size: 0.78rem; color: #64748b; }
    .source-pill {
      display: inline-block;
      padding: 0.2rem 0.55rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 700;
      background: #f1f5f9;
      color: #475569;
    }
    .source-pill.live { background: #ecfdf5; color: #047857; }
    .niche-chip { font-size: 0.82rem; color: #334155; }
    .rev-pill { font-weight: 700; color: #059669; display: block; }
    .rpm-sub { font-size: 0.72rem; color: #64748b; }
    .sync-date { font-size: 0.8rem; color: #64748b; white-space: nowrap; }

    .videos-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(270px, 1fr));
      gap: 1.15rem;
    }
    .video-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      overflow: hidden;
      text-decoration: none;
      color: inherit;
      display: flex;
      flex-direction: column;
      transition: transform 0.2s, box-shadow 0.2s;
    }
    .video-card:hover { transform: translateY(-3px); box-shadow: 0 10px 24px rgba(15, 23, 42, 0.08); }
    .thumb-wrap { position: relative; aspect-ratio: 16 / 9; background: #0f172a; overflow: hidden; }
    .thumb-wrap img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .views-badge {
      position: absolute;
      bottom: 8px;
      right: 8px;
      background: rgba(15, 23, 42, 0.85);
      color: #ffffff;
      font-size: 0.74rem;
      font-weight: 700;
      padding: 0.2rem 0.55rem;
      border-radius: 6px;
    }
    .video-body { padding: 0.9rem 1rem 1rem; display: flex; flex-direction: column; gap: 0.45rem; flex: 1; }
    .vid-channel { font-size: 0.74rem; font-weight: 700; color: #2563eb; }
    .video-body h4 {
      margin: 0;
      font-size: 0.94rem;
      font-weight: 700;
      color: #0f172a;
      line-height: 1.35;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .vid-meta {
      margin-top: auto;
      padding-top: 0.5rem;
      display: flex;
      justify-content: space-between;
      font-size: 0.76rem;
      color: #64748b;
    }
    .watch-cta { color: #059669; font-weight: 700; }

    .cross-promo-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: 1.25rem;
    }
    .promo-card {
      padding: 1.65rem;
      border-radius: 14px;
      color: #ffffff;
      display: flex;
      flex-direction: column;
      gap: 0.7rem;
    }
    .promo-tag { font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.05em; opacity: 0.85; font-weight: 700; }
    .studio-card { background: linear-gradient(135deg, #4f46e5 0%, #312e81 100%); }
    .panchang-card { background: linear-gradient(135deg, #d97706 0%, #78350f 100%); }
    .sibling-card { background: linear-gradient(135deg, #0f766e 0%, #134e4a 100%); }
    .promo-card h3 { margin: 0; font-size: 1.25rem; font-weight: 800; }
    .promo-card p { margin: 0; font-size: 0.92rem; opacity: 0.92; line-height: 1.5; flex: 1; }

    .alert-banner {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
      padding: 0.85rem 1.15rem;
      border-radius: 10px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.9rem;
    }
    .alert-banner.warn { background: #fffbeb; border-color: #fde68a; color: #92400e; }
    .alert-banner.info { background: #ecfdf5; border-color: #a7f3d0; color: #065f46; }
    .alert-close { background: transparent; border: none; color: inherit; cursor: pointer; font-weight: 700; font-size: 1rem; }

    .btn-yt { background: #dc2626; color: #fff; border: none; cursor: pointer; }
    .btn-yt:hover { background: #b91c1c; }

    .workspace-bar {
      background: #fff;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      padding: 1.15rem 1.35rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .workspace-info h3 { margin: 0 0 0.2rem; font-size: 1.02rem; color: #0f172a; }
    .workspace-info p { margin: 0; font-size: 0.84rem; color: #64748b; }
    .oauth-hint { font-size: 0.82rem; color: #475569; font-weight: 500; }
    .connections-strip { display: flex; gap: 0.75rem; flex-wrap: wrap; }
    .conn-pill {
      background: #fff; border: 1px solid #cbd5e1; border-radius: 12px;
      padding: 0.6rem 0.85rem; display: flex; align-items: center; gap: 0.65rem;
    }
    .conn-pill img { width: 32px; height: 32px; border-radius: 50%; object-fit: cover; }
    .conn-meta { display: flex; flex-direction: column; }
    .conn-meta strong { font-size: 0.85rem; color: #0f172a; }
    .conn-meta span { font-size: 0.74rem; color: #64748b; }
    .row-actions { display: flex; gap: 0.35rem; margin-top: 0.3rem; }
    .btn-chip {
      padding: 0.25rem 0.6rem; border-radius: 6px; border: 1px solid #cbd5e1;
      background: #f8fafc; color: #0f172a; font-size: 0.74rem; font-weight: 600; cursor: pointer;
    }
    .btn-chip.danger { color: #b91c1c; border-color: #fecaca; background: #fef2f2; }
  `]
})
export class DashboardComponent implements OnInit {
  readonly analytics = inject(AnalyticsService);
  private ecosystem = inject(EcosystemConfigService);
  private route = inject(ActivatedRoute);
  readonly auth = inject(AuthService);

  readonly channels = signal<ChannelMetrics[]>([]);
  readonly platformSummaries = signal<PlatformSummary[]>([]);
  readonly recentVideos = signal<RecentFeedVideo[]>([]);
  readonly siteConfig = signal<SiteConfig | null>(null);
  readonly loading = signal(true);
  readonly adminDenied = signal(false);
  readonly selectedPlatform = signal<string>('ALL');
  readonly promoSites = computed(() => this.ecosystem.promoSiblingSites().slice(0, 6));

  readonly statusToast = signal('');
  readonly lookupResult = this.analytics.activeLookup;

  readonly oauthConfigured = signal(false);
  readonly oauthConnecting = signal(false);
  readonly youtubeConnections = signal<YouTubeConnectionView[]>([]);

  readonly verifiedCount = computed(() => this.channels().filter(c => c.isVerifiedLive).length);

  readonly availablePlatforms = computed(() => {
    const set = new Set(this.channels().map(c => c.platform));
    return Array.from(set);
  });

  readonly filteredChannels = computed(() => {
    const sel = this.selectedPlatform();
    if (sel === 'ALL') return this.channels();
    return this.channels().filter(c => c.platform === sel);
  });

  readonly totalViewsNumeric = computed(() => {
    const userSum = this.platformSummaries().reduce((acc, s) => acc + s.totalViews, 0);
    if (this.channels().length > 0) return userSum;
    return this.lookupResult()?.channel.totalViewsLast30Days ?? 0;
  });

  readonly totalViewsLabel = computed(() => {
    const sum = this.totalViewsNumeric();
    if (sum >= 1_000_000) return `${(sum / 1_000_000).toFixed(2)}M`;
    if (sum >= 1_000) return `${(sum / 1_000).toFixed(1)}K`;
    return this.loading() ? '…' : `${sum}`;
  });

  readonly totalFollowersLabel = computed(() => {
    const userSum = this.platformSummaries().reduce((acc, s) => acc + s.totalAudience, 0);
    const sum = this.channels().length > 0 ? userSum : (this.lookupResult()?.channel.followersOrSubscribers ?? 0);
    if (sum >= 1_000_000) return `${(sum / 1_000_000).toFixed(2)}M`;
    if (sum >= 1_000) return `${(sum / 1_000).toFixed(1)}K`;
    return this.loading() ? '…' : `${sum}`;
  });

  readonly activeVideoCount = computed(() => {
    if (this.channels().length > 0) return this.recentVideos().length;
    return this.lookupResult()?.channel.recentVideos?.length ?? 0;
  });

  readonly totalRevenue = computed(() => {
    if (this.channels().length > 0) {
      return this.platformSummaries().reduce((acc, s) => acc + s.estimatedEarningsMonthly, 0);
    }
    return this.lookupResult()?.earnings.totalEstimatedMonthlyRevenue ?? 0;
  });

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.loadYouTubeStatus();
      } else {
        this.youtubeConnections.set([]);
      }
    });
  }

  ngOnInit(): void {
    this.route.queryParamMap.subscribe(params => {
      this.adminDenied.set(params.get('denied') === 'admin');
    });
    this.analytics.getYouTubeOAuthPublicConfig().subscribe((cfg) => {
      this.oauthConfigured.set(cfg.isOAuthConfigured);
    });
    this.loadWorkspaceData();
    this.analytics.getSiteConfig().subscribe(cfg => this.siteConfig.set(cfg));
  }

  private loadWorkspaceData(): void {
    this.loading.set(true);
    this.analytics.getChannels().subscribe(ch => this.channels.set(ch));
    this.analytics.getOverview()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe(ov => this.platformSummaries.set(ov));
    this.analytics.getRecentVideos(12).subscribe(v => this.recentVideos.set(v));
  }

  private loadYouTubeStatus(): void {
    this.analytics.getYouTubeOAuthStatus().subscribe((res) => {
      this.oauthConfigured.set(res.isOAuthConfigured);
      this.youtubeConnections.set(res.connections ?? []);
    });
  }

  onChannelSaved(saved: ChannelMetrics): void {
    this.statusToast.set(`Saved ${saved.channelName} (${saved.channelHandle}) to your private workspace.`);
    this.loadWorkspaceData();
  }

  connectYouTube(): void {
    this.oauthConnecting.set(true);
    this.analytics.startYouTubeOAuth('/dashboard').subscribe({
      next: ({ url }) => {
        if (typeof window !== 'undefined' && url) {
          window.location.href = url;
        } else {
          this.oauthConnecting.set(false);
        }
      },
      error: (err) => {
        this.oauthConnecting.set(false);
        this.statusToast.set(err?.error?.message || 'YouTube OAuth is not available yet.');
      }
    });
  }

  syncOAuthConnection(channelId: string): void {
    this.analytics.syncYouTubeConnection(channelId).subscribe({
      next: (ch) => {
        this.statusToast.set(`Synced live metrics for ${ch.channelName}.`);
        this.loadWorkspaceData();
        this.loadYouTubeStatus();
      },
      error: () => this.statusToast.set('Failed to refresh YouTube OAuth connection.')
    });
  }

  disconnectOAuth(channelId: string): void {
    this.analytics.disconnectYouTubeConnection(channelId).subscribe({
      next: () => {
        this.statusToast.set('Disconnected YouTube OAuth connection.');
        this.loadYouTubeStatus();
      }
    });
  }

  refreshUserChannel(id: string): void {
    this.analytics.refreshMyChannel(id).subscribe({
      next: (ch) => {
        this.statusToast.set(`Refreshed ${ch.channelName}.`);
        this.loadWorkspaceData();
      }
    });
  }

  removeUserChannel(id: string): void {
    this.analytics.deleteMyChannel(id).subscribe({
      next: () => {
        this.statusToast.set('Removed channel from your private workspace.');
        this.loadWorkspaceData();
      }
    });
  }

  platformViewShare(views: number): number {
    const total = this.totalViewsNumeric();
    if (total <= 0) return 0;
    return Math.round((views / total) * 100);
  }

  dismissDenied(): void {
    this.adminDenied.set(false);
  }

  getPlatformIcon(platform: string): string {
    switch (platform) {
      case 'YouTube': return '▶️';
      case 'Instagram': return '📸';
      case 'Facebook': return '👥';
      case 'TikTok': return '🎵';
      case 'LinkedIn': return '💼';
      default: return '🌐';
    }
  }
}
