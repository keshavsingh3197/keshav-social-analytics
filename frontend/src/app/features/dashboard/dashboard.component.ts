import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AnalyticsService } from '../../core/services/analytics.service';
import { ChannelMetrics, PlatformSummary } from '../../core/models/analytics.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="dashboard-page">
      <!-- Welcome Hero Banner -->
      <section class="hero-banner">
        <div class="hero-content">
          <h1>Social Channel Performance & Growth Hub</h1>
          <p>
            Track multi-platform engagement across YouTube, Instagram, and Facebook. Maximize audience retention, 
            calculate creator earnings, and funnel viewer traffic directly into your monetized platforms.
          </p>
          <div class="hero-cta-group">
            <a routerLink="/earnings" class="btn btn-primary">💰 Calculate Your Earnings</a>
            <a routerLink="/advisor" class="btn btn-secondary">🚀 View Growth Recommendations</a>
            <a routerLink="/funnel" class="btn btn-outline">🎯 Traffic Funnels</a>
          </div>
        </div>
        <div class="hero-stats-card">
          <div class="stat-pill">
            <span class="stat-num">{{ totalViewsLabel() }}</span>
            <span class="stat-label">30-Day Total Views</span>
          </div>
          <div class="stat-pill">
            <span class="stat-num">{{ totalFollowersLabel() }}</span>
            <span class="stat-label">Combined Followers</span>
          </div>
          <div class="stat-pill">
            <span class="stat-num">\${{ totalRevenue() | number }}</span>
            <span class="stat-label">Est. Monthly Revenue</span>
          </div>
        </div>
      </section>

      <!-- Platform Overview Cards -->
      <section class="section">
        <div class="section-header">
          <h2>Platform Overview</h2>
          <span class="section-tag">Real-time sync</span>
        </div>

        <div class="grid grid-3">
          @for (summary of platformSummaries(); track summary.platform) {
            <div class="platform-card" [attr.data-platform]="summary.platform.toLowerCase()">
              <div class="plat-head">
                <span class="plat-badge">{{ getPlatformIcon(summary.platform) }} {{ summary.platform }}</span>
                <span class="plat-rev">\${{ summary.estimatedEarningsMonthly }}/mo</span>
              </div>
              <div class="plat-body">
                <div class="metric">
                  <span class="m-val">{{ summary.totalAudience | number }}</span>
                  <span class="m-lbl">Audience / Subs</span>
                </div>
                <div class="metric">
                  <span class="m-val">{{ summary.totalViews | number }}</span>
                  <span class="m-lbl">Monthly Views</span>
                </div>
                <div class="metric">
                  <span class="m-val">{{ summary.avgEngagement }}%</span>
                  <span class="m-lbl">Avg Engagement</span>
                </div>
              </div>
            </div>
          }
        </div>
      </section>

      <!-- Connected Channels Table -->
      <section class="section">
        <div class="section-header">
          <h2>Active Channels & Pages</h2>
          <span class="section-tag">Performance Index</span>
        </div>

        <div class="table-container">
          <table class="data-table">
            <thead>
              <tr>
                <th>Channel / Account</th>
                <th>Platform</th>
                <th>Niche</th>
                <th>Subscribers</th>
                <th>30-Day Views</th>
                <th>Engagement</th>
                <th>Avg Duration</th>
                <th>Monthly Growth</th>
              </tr>
            </thead>
            <tbody>
              @for (channel of channels(); track channel.id) {
                <tr>
                  <td>
                    <div class="channel-identity">
                      <strong>{{ channel.channelName }}</strong>
                      <span class="channel-handle">{{ channel.channelHandle }}</span>
                    </div>
                  </td>
                  <td>
                    <span class="badge platform-badge" [attr.data-platform]="channel.platform.toLowerCase()">
                      {{ getPlatformIcon(channel.platform) }} {{ channel.platform }}
                    </span>
                  </td>
                  <td>{{ channel.niche }}</td>
                  <td><strong>{{ channel.followersOrSubscribers | number }}</strong></td>
                  <td>{{ channel.totalViewsLast30Days | number }}</td>
                  <td>
                    <span class="engagement-pill">{{ channel.engagementRatePercentage }}%</span>
                  </td>
                  <td>{{ channel.averageViewDurationMinutes }} mins</td>
                  <td>
                    <span class="growth-indicator positive">+{{ channel.growthRateMonthlyPercentage }}%</span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Sibling Cross-Promotion & Traffic Generation Blocks -->
      <section class="cross-promo-grid">
        <div class="promo-card studio-card">
          <div class="promo-tag">Featured Sibling App</div>
          <h3>🎬 Keshav AnimStudio AI</h3>
          <p>Generate viral 2D/3D AI animated stories and YouTube shorts in minutes. Directly boost video output and channel upload frequency.</p>
          <a href="https://studio.keshavsingh.in" target="_blank" rel="noopener" class="btn btn-accent">Open Studio AI →</a>
        </div>

        <div class="promo-card panchang-card">
          <div class="promo-tag">Daily Cultural Hub</div>
          <h3>🕉️ Hindu Panchang & Aaj Ka Itihaas</h3>
          <p>High-engagement daily Vedic calendar, spiritual video feeds, and historical incidents. Drive massive recurring daily visitors.</p>
          <a href="https://panchang.keshavsingh.in" target="_blank" rel="noopener" class="btn btn-accent">Explore Panchang Hub →</a>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .dashboard-page {
      max-width: 1300px;
      margin: 0 auto;
      padding: 1.5rem 1rem 3rem;
      display: flex;
      flex-direction: column;
      gap: 2rem;
    }
    .hero-banner {
      background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
      color: #ffffff;
      border-radius: 16px;
      padding: 2.2rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 2rem;
      flex-wrap: wrap;
      box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.3);
    }
    .hero-content {
      max-width: 680px;
    }
    .hero-content h1 {
      font-size: 2.1rem;
      font-weight: 800;
      line-height: 1.2;
      margin: 0 0 0.8rem;
    }
    .hero-content p {
      color: #94a3b8;
      font-size: 1.05rem;
      line-height: 1.6;
      margin: 0 0 1.5rem;
    }
    .hero-cta-group {
      display: flex;
      gap: 0.8rem;
      flex-wrap: wrap;
    }
    .hero-stats-card {
      display: flex;
      flex-direction: column;
      gap: 0.9rem;
      background: rgba(255, 255, 255, 0.05);
      border: 1px solid rgba(255, 255, 255, 0.1);
      padding: 1.5rem;
      border-radius: 12px;
      min-width: 240px;
    }
    .stat-pill {
      display: flex;
      flex-direction: column;
    }
    .stat-num {
      font-size: 1.6rem;
      font-weight: 800;
      color: #38bdf8;
    }
    .stat-label {
      font-size: 0.8rem;
      color: #cbd5e1;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .btn {
      padding: 0.65rem 1.25rem;
      border-radius: 8px;
      font-weight: 600;
      text-decoration: none;
      display: inline-block;
      font-size: 0.95rem;
      cursor: pointer;
      border: none;
      transition: all 0.2s ease;
    }
    .btn-primary { background: #2563eb; color: #ffffff; }
    .btn-primary:hover { background: #1d4ed8; }
    .btn-secondary { background: #059669; color: #ffffff; }
    .btn-secondary:hover { background: #047857; }
    .btn-outline { background: transparent; color: #ffffff; border: 1px solid #475569; }
    .btn-outline:hover { background: #334155; }
    .btn-accent { background: #ffffff; color: #0f172a; font-weight: 700; }
    .btn-accent:hover { background: #f1f5f9; }

    .section {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .section-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .section-header h2 {
      font-size: 1.4rem;
      font-weight: 700;
      color: #0f172a;
      margin: 0;
    }
    .section-tag {
      font-size: 0.8rem;
      background: #f1f5f9;
      color: #64748b;
      padding: 0.2rem 0.6rem;
      border-radius: 9999px;
      font-weight: 600;
    }
    .grid { display: grid; gap: 1.25rem; }
    .grid-3 { grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); }

    .platform-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
      box-shadow: 0 2px 4px rgba(0,0,0,0.03);
      transition: transform 0.2s;
    }
    .platform-card:hover { transform: translateY(-2px); }
    .plat-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.2rem;
      padding-bottom: 0.75rem;
      border-bottom: 1px solid #f1f5f9;
    }
    .plat-badge {
      font-weight: 700;
      font-size: 1.05rem;
      color: #1e293b;
    }
    .plat-rev {
      font-weight: 700;
      color: #059669;
      font-size: 1rem;
    }
    .plat-body {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 0.5rem;
      text-align: center;
    }
    .metric .m-val {
      display: block;
      font-size: 1.2rem;
      font-weight: 700;
      color: #0f172a;
    }
    .metric .m-lbl {
      font-size: 0.75rem;
      color: #64748b;
    }

    .table-container {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      overflow-x: auto;
      box-shadow: 0 1px 3px rgba(0,0,0,0.02);
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
      font-size: 0.9rem;
    }
    .data-table th {
      background: #f8fafc;
      color: #475569;
      font-weight: 600;
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #e2e8f0;
    }
    .data-table td {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      color: #334155;
    }
    .channel-identity {
      display: flex;
      flex-direction: column;
    }
    .channel-handle {
      font-size: 0.8rem;
      color: #64748b;
    }
    .platform-badge {
      padding: 0.2rem 0.5rem;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      background: #e2e8f0;
    }
    .engagement-pill {
      background: #dbeafe;
      color: #1d4ed8;
      font-weight: 700;
      padding: 0.2rem 0.5rem;
      border-radius: 6px;
    }
    .growth-indicator.positive {
      color: #059669;
      font-weight: 700;
    }

    .cross-promo-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 1.25rem;
    }
    .promo-card {
      padding: 1.75rem;
      border-radius: 14px;
      color: #ffffff;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .promo-tag {
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      opacity: 0.85;
      font-weight: 700;
    }
    .studio-card { background: linear-gradient(135deg, #4f46e5 0%, #312e81 100%); }
    .panchang-card { background: linear-gradient(135deg, #d97706 0%, #78350f 100%); }
    .promo-card h3 { margin: 0; font-size: 1.35rem; font-weight: 800; }
    .promo-card p { margin: 0; font-size: 0.95rem; opacity: 0.9; line-height: 1.5; }
  `]
})
export class DashboardComponent implements OnInit {
  private analytics = inject(AnalyticsService);

  readonly channels = signal<ChannelMetrics[]>([]);
  readonly platformSummaries = signal<PlatformSummary[]>([]);

  readonly totalViewsLabel = computed(() => {
    const sum = this.platformSummaries().reduce((acc, s) => acc + s.totalViews, 0);
    if (sum >= 1_000_000) return `${(sum / 1_000_000).toFixed(2)}M+`;
    if (sum >= 1_000) return `${(sum / 1_000).toFixed(1)}K`;
    return sum ? `${sum}` : '1.08M+';
  });

  readonly totalFollowersLabel = computed(() => {
    const sum = this.platformSummaries().reduce((acc, s) => acc + s.totalAudience, 0);
    if (sum >= 1_000) return `${(sum / 1_000).toFixed(1)}K`;
    return sum ? `${sum}` : '93.2K';
  });

  readonly totalRevenue = computed(() => {
    const sum = this.platformSummaries().reduce((acc, s) => acc + s.estimatedEarningsMonthly, 0);
    return sum || 2720;
  });

  ngOnInit(): void {
    this.analytics.getChannels().subscribe(ch => this.channels.set(ch));
    this.analytics.getOverview().subscribe(ov => this.platformSummaries.set(ov));
  }

  getPlatformIcon(platform: string): string {
    switch (platform) {
      case 'YouTube': return '▶️';
      case 'Instagram': return '📸';
      case 'Facebook': return '👥';
      case 'TikTok': return '🎵';
      default: return '🌐';
    }
  }
}
