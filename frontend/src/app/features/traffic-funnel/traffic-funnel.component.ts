import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AnalyticsService } from '../../core/services/analytics.service';
import { EcosystemConfigService } from '../../core/services/ecosystem-config.service';
import { AuthService } from '../../core/services/auth.service';
import { AdSlotComponent } from '../shared/ad-slot/ad-slot.component';
import { FunnelCampaign, SiteConfig } from '../../core/models/analytics.models';

@Component({
  selector: 'app-traffic-funnel',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, AdSlotComponent],
  template: `
    <div class="funnel-page">
      <div class="header-box">
        <h1>🎯 Traffic Funnel & Link Conversion Engine</h1>
        <p>
          Convert video viewers and social visitors into tracked website sessions and conversions.
          Each signed-in creator manages their own private campaigns and short tracking URLs (<code>/t/...</code>).
        </p>
      </div>

      <!-- Quick Funnel Presets -->
      <section class="preset-section">
        <h3>🚀 {{ ecosystemTitle() }}</h3>
        <p class="section-sub">
          @if (auth.isAuthenticated()) {
            Generate or copy trackable referral links in your private funnel workspace:
          } @else {
            Featured connected destinations (sign in to create your own tracked referral links):
          }
        </p>

        <div class="preset-grid">
          @for (site of promoSites(); track site.key) {
            <div class="preset-card">
              <div class="preset-icon">{{ site.icon || '🌐' }}</div>
              <div class="preset-details">
                <strong>{{ site.name }}</strong>
                <span>Target: {{ site.url.replace('https://', '') }}</span>
              </div>
              @if (findCampaignByDestination(site.url); as matched) {
                <button (click)="copyLink(matched.trackingSlug)" class="btn-preset">
                  📋 Copy Link
                </button>
              } @else if (auth.isAuthenticated()) {
                <button (click)="createEcosystemCampaign(site.name + ' Funnel', site.url, 'yt-' + site.key)" class="btn-preset">
                  Generate Link
                </button>
              } @else {
                <a [href]="site.url" target="_blank" rel="noopener" class="btn-preset-link">Visit →</a>
              }
            </div>
          }
        </div>
      </section>

      <!-- Create Custom Campaign Bar (Authenticated Users - Private per User) -->
      @if (auth.isAuthenticated()) {
        <section class="create-card">
          <h3>Create New Private Tracked Campaign Link</h3>
          <div class="form-row">
            <input 
              type="text" 
              [ngModel]="campaignTitle()" 
              (ngModelChange)="campaignTitle.set($event)"
              placeholder="Campaign Title (e.g. YouTube Video #45 Link)" 
              class="f-input" />
            <input 
              type="text" 
              [ngModel]="campaignSource()" 
              (ngModelChange)="campaignSource.set($event)"
              placeholder="Source (e.g. YouTube Description)" 
              class="f-input" />
            <input 
              type="text" 
              [ngModel]="campaignDestination()" 
              (ngModelChange)="campaignDestination.set($event)"
              placeholder="Destination URL (https://...)" 
              class="f-input" />
            <button (click)="submitCampaign()" class="btn-create">Create Funnel</button>
          </div>
          @if (statusMessage(); as msg) {
            <div class="status-toast">{{ msg }}</div>
          }
        </section>
      } @else {
        <section class="create-card admin-lock-card">
          <div class="lock-row">
            <span>🔐 Sign in with SSO to create your own tracked <code>/t/...</code> funnel links and view your private click/conversion metrics.</span>
            <button (click)="auth.loginRedirect()" class="btn-preset">Sign in with SSO</button>
          </div>
          @if (statusMessage(); as msg) {
            <div class="status-toast">{{ msg }}</div>
          }
        </section>
      }

      <app-ad-slot placement="infeed" />

      <!-- Active Campaigns Table -->
      <section class="campaigns-section">
        <div class="campaigns-head">
          <h3>Active Traffic Campaigns & Conversion Metrics</h3>
          @if (auth.isAdmin()) {
            <a routerLink="/admin" class="manage-link">Manage all funnels in Admin →</a>
          }
        </div>
        <div class="table-wrap">
          <table class="funnel-table">
            <thead>
              <tr>
                <th>Campaign</th>
                <th>Source Platform</th>
                <th>Destination Target</th>
                <th>Tracked Link</th>
                <th>Clicks</th>
                <th>Conversions</th>
                <th>Conversion Rate</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (camp of campaigns(); track camp.id) {
                <tr>
                  <td><strong>{{ camp.title }}</strong></td>
                  <td><span class="source-tag">{{ camp.sourcePlatform }}</span></td>
                  <td><a [href]="camp.destinationUrl" target="_blank" rel="noopener" class="dest-link">{{ camp.destinationUrl }}</a></td>
                  <td>
                    <a [routerLink]="['/t', camp.trackingSlug]" class="slug-link">
                      <code class="slug-code">{{ trackingUrl(camp.trackingSlug) }}</code>
                    </a>
                  </td>
                  <td><strong>{{ camp.totalClicks | number }}</strong></td>
                  <td>{{ camp.conversions | number }}</td>
                  <td>
                    <span class="c-rate">{{ camp.conversionRatePercentage }}%</span>
                  </td>
                  <td>
                    <div class="row-btns">
                      <button (click)="copyLink(camp.trackingSlug)" class="btn-copy">📋 Copy</button>
                      @if (auth.isAuthenticated()) {
                        <button (click)="recordUserConversion(camp)" class="btn-copy conv">+1 Conv</button>
                        <button (click)="deleteUserCampaign(camp)" class="btn-copy del">Delete</button>
                      }
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Video Description Template Generator -->
      <section class="description-template-card">
        <h3>📝 Pre-formatted YouTube Video Description Template</h3>
        <p class="section-sub">Dynamically built from your active ecosystem URLs & funnel links:</p>
        <div class="template-box">
          <pre>{{ videoDescriptionSnippet() }}</pre>
        </div>
        <button (click)="copySnippet()" class="btn-snippet">📋 Copy Description Snippet</button>
      </section>
    </div>
  `,
  styles: [`
    .funnel-page {
      max-width: 1300px;
      margin: 0 auto;
      padding: 1.5rem 1rem 3rem;
      display: flex;
      flex-direction: column;
      gap: 1.75rem;
    }
    .header-box h1 {
      font-size: 1.9rem;
      font-weight: 800;
      color: #0f172a;
      margin: 0 0 0.5rem;
    }
    .header-box p {
      color: #64748b;
      margin: 0;
      font-size: 1rem;
      max-width: 850px;
      line-height: 1.5;
    }

    .preset-section h3, .campaigns-section h3 {
      margin: 0 0 0.3rem;
      font-size: 1.2rem;
      color: #0f172a;
    }
    .section-sub {
      margin: 0 0 1rem;
      color: #64748b;
      font-size: 0.9rem;
    }
    .preset-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 1rem;
    }
    .preset-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1rem 1.25rem;
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .preset-icon { font-size: 1.75rem; }
    .preset-details {
      display: flex;
      flex-direction: column;
      flex: 1;
    }
    .preset-details strong { font-size: 0.95rem; color: #1e293b; }
    .preset-details span { font-size: 0.8rem; color: #64748b; }
    .btn-preset {
      background: #eff6ff;
      color: #2563eb;
      border: 1px solid #bfdbfe;
      padding: 0.4rem 0.8rem;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
    }
    .btn-preset:hover { background: #dbeafe; }

    .create-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
    }
    .create-card h3 { margin: 0 0 0.8rem; font-size: 1.1rem; color: #1e293b; }
    .form-row {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
    }
    .f-input {
      flex: 1;
      min-width: 200px;
      padding: 0.6rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
    }
    .btn-create {
      background: #059669;
      color: white;
      border: none;
      padding: 0.6rem 1.2rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-create:hover { background: #047857; }

    .table-wrap {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      overflow-x: auto;
    }
    .funnel-table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
      font-size: 0.9rem;
    }
    .funnel-table th {
      background: #f8fafc;
      color: #475569;
      font-weight: 600;
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #e2e8f0;
    }
    .funnel-table td {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      color: #334155;
    }
    .source-tag {
      background: #f1f5f9;
      color: #475569;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-size: 0.8rem;
    }
    .dest-link {
      color: #2563eb;
      text-decoration: none;
      font-weight: 500;
    }
    .slug-code {
      background: #f1f5f9;
      padding: 0.2rem 0.4rem;
      border-radius: 4px;
      color: #0f172a;
      font-size: 0.8rem;
    }
    .c-rate {
      color: #059669;
      font-weight: 700;
    }
    .btn-copy {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      padding: 0.3rem 0.6rem;
      border-radius: 6px;
      font-size: 0.8rem;
      cursor: pointer;
    }
    .btn-copy:hover { background: #e2e8f0; }

    .description-template-card {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
    }
    .template-box {
      background: #1e293b;
      color: #e2e8f0;
      padding: 1rem;
      border-radius: 8px;
      overflow-x: auto;
      margin-bottom: 0.85rem;
    }
    .template-box pre {
      margin: 0;
      font-family: Consolas, monospace;
      font-size: 0.88rem;
      line-height: 1.5;
    }
    .btn-snippet {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.55rem 1.1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-snippet:hover { background: #1d4ed8; }
    .status-toast {
      margin-top: 0.75rem;
      background: #ecfdf5;
      color: #065f46;
      border: 1px solid #a7f3d0;
      padding: 0.5rem 0.85rem;
      border-radius: 6px;
      font-size: 0.85rem;
      font-weight: 600;
    }
    .btn-preset-link {
      background: #e2e8f0;
      color: #1e293b;
      text-decoration: none;
      padding: 0.4rem 0.75rem;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      white-space: nowrap;
    }
    .lock-row { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; font-size: 0.9rem; color: #475569; }
    .campaigns-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 0.75rem; }
    .campaigns-head h3 { margin: 0; }
    .manage-link { color: #2563eb; text-decoration: none; font-weight: 600; font-size: 0.88rem; }
    .slug-link { text-decoration: none; }
    .row-btns { display: flex; gap: 0.35rem; flex-wrap: wrap; }
    .btn-copy.conv { background: #ecfdf5; color: #065f46; border-color: #a7f3d0; }
    .btn-copy.del { background: #fef2f2; color: #b91c1c; border-color: #fecaca; }
  `]
})
export class TrafficFunnelComponent implements OnInit {
  private analytics = inject(AnalyticsService);
  private ecosystem = inject(EcosystemConfigService);
  readonly auth = inject(AuthService);

  readonly campaigns = signal<FunnelCampaign[]>([]);
  readonly siteConfig = signal<SiteConfig | null>(null);
  readonly promoSites = this.ecosystem.promoSiblingSites;
  readonly campaignTitle = signal('');
  readonly campaignSource = signal('YouTube Description');
  readonly campaignDestination = signal('');
  readonly statusMessage = signal<string | null>(null);

  readonly ecosystemTitle = computed(() => {
    if (this.auth.isAdmin()) {
      const creator = this.ecosystem.creatorName();
      return creator ? `${creator} Ecosystem Funnels` : 'Connected Ecosystem Funnels';
    }
    return 'Quick Funnel Destinations';
  });

  readonly videoDescriptionSnippet = computed(() => {
    const cfg = this.siteConfig();
    const header = cfg?.youtubeSnippetHeader || 'Explore Connected Apps & Resources:';
    const footer = cfg?.youtubeSnippetFooter || 'Subscribe for regular updates & new releases!';
    const channelHashtags = this.auth.isAdmin()
      ? (this.ecosystem.primaryYouTubeChannel()?.hashtags?.map(h => `#${h.replace(/^#+/, '')}`).join(' ') || '')
      : '';
    const tags = cfg?.hashtags || channelHashtags;
    const lines = this.promoSites().map(site => {
      const camp = this.findCampaignByDestination(site.url);
      const targetUrl = camp ? this.trackingUrl(camp.trackingSlug) : site.url;
      return `${site.icon || '🔗'} ${site.name}: ${targetUrl}`;
    });
    return `${header}\n${lines.join('\n')}\n\n${footer}${tags ? '\n' + tags : ''}`;
  });

  ngOnInit(): void {
    this.analytics.getCampaigns().subscribe(c => this.campaigns.set(c));
    this.analytics.getSiteConfig().subscribe(cfg => {
      this.siteConfig.set(cfg);
      if (cfg?.defaultDestinationBaseUrl) {
        this.campaignDestination.set(cfg.defaultDestinationBaseUrl);
      } else if (this.promoSites().length > 0) {
        this.campaignDestination.set(this.promoSites()[0].url);
      }
    });
  }

  findCampaignByDestination(url: string): FunnelCampaign | undefined {
    const clean = url.replace(/\/+$/, '').toLowerCase();
    return this.campaigns().find(c => c.destinationUrl.replace(/\/+$/, '').toLowerCase() === clean);
  }

  trackingUrl(slug: string): string {
    const origin = typeof window !== 'undefined' && window.location?.origin
      ? window.location.origin
      : 'https://social.keshavsingh.in';
    return `${origin}/t/${slug}`;
  }

  submitCampaign(): void {
    const title = this.campaignTitle().trim();
    const destinationUrl = this.campaignDestination().trim();
    const sourcePlatform = this.campaignSource().trim() || 'YouTube Description';
    if (!title || !destinationUrl) return;

    const payload: FunnelCampaign = {
      title,
      sourcePlatform,
      destinationUrl,
      trackingSlug: 'c-' + Math.random().toString(36).substring(2, 7),
      totalClicks: 0,
      conversions: 0,
      conversionRatePercentage: 0,
      isActive: true
    };

    this.analytics.createCampaign(payload).subscribe({
      next: (camp) => {
        this.campaigns.update(list => [camp, ...list]);
        this.campaignTitle.set('');
        this.campaignSource.set('YouTube Description');
        this.campaignDestination.set(this.siteConfig()?.defaultDestinationBaseUrl || this.promoSites()[0]?.url || '');
        this.showStatus(`Created funnel campaign "${camp.title}"`);
      },
      error: (err) => {
        this.showStatus(err?.status === 403 || err?.status === 401
          ? 'Admin sign-in is required to create funnels.'
          : 'Failed to create campaign on server.');
      }
    });
  }

  createEcosystemCampaign(title: string, destination: string, slug: string): void {
    const existing = this.campaigns().find(c => c.trackingSlug === slug);
    if (!existing) {
      this.analytics.createCampaign({
        title,
        sourcePlatform: 'YouTube / Instagram',
        destinationUrl: destination,
        trackingSlug: slug,
        totalClicks: 0,
        conversions: 0,
        conversionRatePercentage: 0,
        isActive: true
      }).subscribe({
        next: (c) => {
          this.campaigns.update(list => [c, ...list]);
          this.copyLink(c.trackingSlug);
        },
        error: () => this.showStatus('Admin role required to register new funnel links.')
      });
    } else {
      this.copyLink(existing.trackingSlug);
    }
  }

  copyLink(slug: string): void {
    const url = this.trackingUrl(slug);
    navigator.clipboard?.writeText(url);
    this.showStatus(`Copied tracked link: ${url}`);
  }

  recordUserConversion(camp: FunnelCampaign): void {
    if (!camp.id) return;
    this.analytics.recordUserConversion(camp.id).subscribe({
      next: (updated) => {
        this.campaigns.update(list => list.map(c => c.id === updated.id ? updated : c));
        this.showStatus(`Recorded conversion for "${camp.title}"`);
      }
    });
  }

  deleteUserCampaign(camp: FunnelCampaign): void {
    if (!camp.id) return;
    this.analytics.deleteUserCampaign(camp.id).subscribe({
      next: () => {
        this.campaigns.update(list => list.filter(c => c.id !== camp.id));
        this.showStatus(`Removed funnel "${camp.title}"`);
      }
    });
  }

  copySnippet(): void {
    navigator.clipboard?.writeText(this.videoDescriptionSnippet());
    this.showStatus('Copied YouTube Description Template to clipboard!');
  }

  private showStatus(msg: string): void {
    this.statusMessage.set(msg);
    setTimeout(() => {
      if (this.statusMessage() === msg) {
        this.statusMessage.set(null);
      }
    }, 4000);
  }
}
