import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { ChannelMetrics, ChannelSearchCandidate, SocialPlatform } from '../../../core/models/analytics.models';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-channel-analyzer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="analyzer-card">
      <div class="section-header">
        <div>
          <h2>🔍 Instant Channel Search &amp; Live Analyzer</h2>
          <span class="section-sub">Type any channel name, &#64;handle, UC... ID, or URL to inspect live metrics, recent uploads, RPM earnings &amp; growth actions</span>
        </div>
        <span class="section-tag">Live Public Lookup</span>
      </div>

      <form class="analyzer-form" (ngSubmit)="analyzeChannel()">
        <select [(ngModel)]="lookupPlatform" name="lookupPlatform" class="form-select">
          <option value="YouTube">▶ YouTube</option>
          <option value="Instagram">📸 Instagram</option>
          <option value="Facebook">👥 Facebook</option>
          <option value="TikTok">🎵 TikTok</option>
          <option value="LinkedIn">💼 LinkedIn</option>
          <option value="Twitter">𝕏 Twitter</option>
        </select>

        <div class="search-input-wrap">
          <input
            type="text"
            [(ngModel)]="lookupQuery"
            name="lookupQuery"
            (ngModelChange)="onSearchInput($event)"
            placeholder="Search YouTube channel name or paste @handle / URL (e.g. @veritasium)"
            class="form-input"
            autocomplete="off" />
          @if (searchingCandidates()) {
            <span class="input-spinner">Searching...</span>
          }

          @if (searchCandidates().length > 0) {
            <div class="search-dropdown">
              @for (cand of searchCandidates(); track cand.externalChannelId + cand.channelHandle) {
                <button type="button" class="candidate-item" (click)="selectCandidate(cand)">
                  @if (cand.avatarUrl) {
                    <img [src]="cand.avatarUrl" [alt]="cand.channelName" class="cand-avatar" loading="lazy" decoding="async" />
                  } @else {
                    <div class="cand-avatar fallback">{{ getPlatformIcon(cand.platform) }}</div>
                  }
                  <div class="cand-info">
                    <strong>{{ cand.channelName }}</strong>
                    <span>{{ cand.channelHandle || cand.externalChannelId }} • {{ cand.followersOrSubscribers | number }} subs</span>
                  </div>
                  <span class="cand-cta">Analyze -></span>
                </button>
              }
            </div>
          }
        </div>

        <input
          type="text"
          [(ngModel)]="lookupNiche"
          name="lookupNiche"
          placeholder="Optional niche (e.g. Tech, Finance)"
          class="form-input niche-input" />

        <button type="submit" class="btn-analyze" [disabled]="analyzing() || !lookupQuery.trim()">
          @if (analyzing()) { <span class="btn-spin" aria-hidden="true"></span>Analyzing... } @else { ⚡ Analyze Live }
        </button>
      </form>

      @if (lookupError()) {
        <div class="inline-error">{{ lookupError() }}</div>
      }

      @if (lookupResult(); as res) {
        <div class="lookup-result-panel">
          <div class="lookup-header">
            <div class="channel-cell">
              @if (res.channel.avatarUrl) {
                <img [src]="res.channel.avatarUrl" [alt]="res.channel.channelName" class="ch-avatar" loading="lazy" decoding="async" />
              } @else {
                <div class="ch-avatar fallback">{{ getPlatformIcon(res.channel.platform) }}</div>
              }
              <div class="channel-identity">
                <div class="title-row">
                  @if (res.channel.channelUrl) {
                    <a [href]="res.channel.channelUrl" target="_blank" rel="noopener" class="ch-link">{{ res.channel.channelName }} ↗</a>
                  } @else {
                    <strong class="ch-link">{{ res.channel.channelName }}</strong>
                  }
                  <span class="source-pill" [class.live]="res.resolvedLive">
                    {{ res.resolvedLive ? '● LIVE' : 'PROFILE' }} • {{ res.channel.dataSource }}
                  </span>
                </div>
                <span class="channel-handle">{{ res.channel.channelHandle }} • {{ res.channel.platform }} • {{ res.channel.niche }}</span>
                <small class="res-summary">{{ res.resolutionSummary }}</small>
              </div>
            </div>

            <div class="lookup-actions">
              <a routerLink="/earnings" class="btn-mini primary">💰 Open in Calculator</a>
              <a
                routerLink="/advisor"
                [queryParams]="{ handle: res.channel.channelHandle || res.channel.channelName, platform: res.channel.platform }"
                class="btn-mini">🚀 Full Growth Audit</a>
              @if (auth.isAuthenticated()) {
                <button type="button" class="btn-mini save" (click)="saveAnalyzedChannel()" [disabled]="savingChannel()">
                  @if (savingChannel()) { <span class="btn-spin" aria-hidden="true"></span>Saving... } @else { + Save to My Channels }
                </button>
              } @else {
                <button type="button" class="btn-mini" (click)="auth.loginRedirect()">🔐 Sign in to Save</button>
              }
            </div>
          </div>

          <div class="lookup-metrics-grid">
            <div class="mini-kpi">
              <span>Subscribers / Followers</span>
              <strong>{{ res.channel.followersOrSubscribers | number }}</strong>
            </div>
            <div class="mini-kpi">
              <span>Est. 30-Day Views</span>
              <strong>{{ res.channel.totalViewsLast30Days | number }}</strong>
            </div>
            <div class="mini-kpi">
              <span>Engagement Rate</span>
              <strong>{{ res.channel.engagementRatePercentage }}%</strong>
            </div>
            <div class="mini-kpi">
              <span>Est. Monthly Revenue</span>
              <strong class="green">\${{ res.earnings.totalEstimatedMonthlyRevenue | number:'1.0-0' }} (\${{ res.channel.rpmUsd }} RPM)</strong>
            </div>
            <div class="mini-kpi">
              <span>Growth Health Score</span>
              <strong>{{ res.audit.healthScoreOutOf100 }} / 100</strong>
            </div>
          </div>

          @if (res.autoInsights.length > 0) {
            <div class="insights-box">
              <strong>✨ Automated Creator Intelligence</strong>
              <ul>
                @for (tip of res.autoInsights; track tip) {
                  <li>{{ tip }}</li>
                }
              </ul>
            </div>
          }

          @if (res.channel.recentVideos && res.channel.recentVideos.length > 0) {
            <div class="lookup-videos-row">
              @for (v of res.channel.recentVideos.slice(0, 4); track v.videoId) {
                <a [href]="v.url" target="_blank" rel="noopener" class="mini-video-chip">
                  @if (v.thumbnailUrl) {
                    <img [src]="v.thumbnailUrl" [alt]="v.title" loading="lazy" />
                  }
                  <div>
                    <strong>{{ v.title }}</strong>
                    <span>{{ v.views | number }} views • {{ v.publishedAt | date:'mediumDate' }}</span>
                  </div>
                </a>
              }
            </div>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    .analyzer-card {
      background: #fff;
      border: 1px solid #e2e8f0;
      border-radius: 18px;
      padding: 1.4rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .section-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; }
    .section-header h2 { margin: 0 0 0.25rem; font-size: 1.25rem; color: #0f172a; }
    .section-sub { font-size: 0.85rem; color: #64748b; }
    .section-tag {
      background: #eff6ff; color: #1d4ed8; font-size: 0.76rem; font-weight: 700;
      padding: 0.3rem 0.75rem; border-radius: 999px; white-space: nowrap;
    }
    .analyzer-form { display: flex; gap: 0.7rem; flex-wrap: wrap; align-items: stretch; }
    .form-select {
      padding: 0.7rem 0.9rem; border: 1px solid #cbd5e1; border-radius: 10px;
      font-size: 0.9rem; font-weight: 600; color: #0f172a; background: #fff;
    }
    .search-input-wrap { position: relative; flex: 2; min-width: 260px; }
    .form-input {
      width: 100%; box-sizing: border-box; padding: 0.72rem 0.95rem;
      border: 1px solid #cbd5e1; border-radius: 10px; font-size: 0.92rem;
    }
    .form-input.niche-input { flex: 1; min-width: 180px; max-width: 240px; }
    .btn-analyze {
      padding: 0.7rem 1.2rem; border-radius: 10px; border: none; background: #2563eb;
      color: #fff; font-weight: 700; cursor: pointer; font-size: 0.9rem;
    }
    .btn-analyze:disabled { opacity: 0.55; cursor: not-allowed; }
    .input-spinner { position: absolute; right: 12px; top: 50%; transform: translateY(-50%); font-size: 0.75rem; color: #64748b; }
    .search-dropdown {
      position: absolute; left: 0; right: 0; top: calc(100% + 4px); background: #fff;
      border: 1px solid #cbd5e1; border-radius: 12px; box-shadow: 0 14px 32px rgba(15, 23, 42, 0.14);
      z-index: 40; overflow: hidden;
    }
    .candidate-item {
      width: 100%; display: flex; align-items: center; gap: 0.75rem; padding: 0.65rem 0.9rem;
      background: #fff; border: none; border-bottom: 1px solid #f1f5f9; text-align: left; cursor: pointer;
    }
    .candidate-item:hover { background: #f8fafc; }
    .cand-avatar { width: 34px; height: 34px; border-radius: 50%; object-fit: cover; flex-shrink: 0; }
    .cand-avatar.fallback { background: #e2e8f0; display: flex; align-items: center; justify-content: center; font-size: 0.95rem; }
    .cand-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    .cand-info strong { font-size: 0.88rem; color: #0f172a; }
    .cand-info span { font-size: 0.76rem; color: #64748b; }
    .cand-cta { font-size: 0.78rem; font-weight: 700; color: #2563eb; white-space: nowrap; }
    .inline-error { color: #b91c1c; font-size: 0.86rem; font-weight: 600; }
    .lookup-result-panel {
      background: #f8fafc; border: 1px solid #dbeafe; border-radius: 14px;
      padding: 1.15rem; display: flex; flex-direction: column; gap: 1rem;
    }
    .lookup-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; }
    .channel-cell { display: flex; align-items: center; gap: 0.85rem; }
    .ch-avatar { width: 48px; height: 48px; border-radius: 50%; object-fit: cover; flex-shrink: 0; }
    .ch-avatar.fallback { background: #e2e8f0; display: flex; align-items: center; justify-content: center; font-size: 1.25rem; }
    .channel-identity { display: flex; flex-direction: column; gap: 0.15rem; }
    .title-row { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .ch-link { font-size: 1.08rem; font-weight: 800; color: #0f172a; text-decoration: none; }
    .source-pill { font-size: 0.72rem; font-weight: 700; padding: 0.18rem 0.55rem; border-radius: 999px; background: #e2e8f0; color: #334155; }
    .source-pill.live { background: #dcfce7; color: #166534; }
    .channel-handle { font-size: 0.82rem; color: #475569; }
    .res-summary { color: #64748b; font-size: 0.78rem; }
    .lookup-actions { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .btn-mini {
      padding: 0.48rem 0.85rem; border-radius: 8px; font-size: 0.8rem; font-weight: 700;
      border: 1px solid #cbd5e1; background: #fff; color: #0f172a; text-decoration: none; cursor: pointer;
    }
    .btn-mini.primary { background: #2563eb; color: #fff; border-color: #2563eb; }
    .btn-mini.save { background: #059669; color: #fff; border-color: #059669; }
    .lookup-metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 0.7rem; }
    .mini-kpi {
      background: #fff; border: 1px solid #e2e8f0; border-radius: 10px;
      padding: 0.75rem 0.9rem; display: flex; flex-direction: column; gap: 0.2rem;
    }
    .mini-kpi span { font-size: 0.74rem; color: #64748b; font-weight: 600; }
    .mini-kpi strong { font-size: 1.02rem; color: #0f172a; }
    .mini-kpi strong.green { color: #059669; }
    .insights-box { background: #fff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.85rem 1rem; font-size: 0.85rem; }
    .insights-box ul { margin: 0.45rem 0 0; padding-left: 1.15rem; color: #334155; display: flex; flex-direction: column; gap: 0.3rem; }
    .lookup-videos-row { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0.7rem; }
    .mini-video-chip {
      display: flex; gap: 0.6rem; align-items: center; background: #fff;
      border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.5rem; text-decoration: none; color: inherit;
    }
    .mini-video-chip img { width: 72px; height: 42px; border-radius: 6px; object-fit: cover; flex-shrink: 0; }
    .mini-video-chip strong { font-size: 0.78rem; color: #0f172a; display: block; line-height: 1.25; }
    .mini-video-chip span { font-size: 0.7rem; color: #64748b; }
  `]
})
export class ChannelAnalyzerComponent implements OnInit, OnDestroy {
  readonly analytics = inject(AnalyticsService);
  readonly auth = inject(AuthService);
  readonly channelSaved = output<ChannelMetrics>();

  private readonly searchTerms$ = new Subject<string>();
  private searchSub?: Subscription;

  lookupPlatform: SocialPlatform = 'YouTube';
  lookupQuery = '';
  lookupNiche = '';

  readonly searchCandidates = signal<ChannelSearchCandidate[]>([]);
  readonly searchingCandidates = signal(false);
  readonly analyzing = signal(false);
  readonly savingChannel = signal(false);
  readonly lookupError = signal('');
  readonly lookupResult = this.analytics.activeLookup;

  ngOnInit(): void {
    this.searchSub = this.searchTerms$
      .pipe(
        debounceTime(320),
        distinctUntilChanged(),
        switchMap((term) => {
          const clean = term.trim();
          if (clean.length < 2 || this.lookupPlatform !== 'YouTube') {
            this.searchingCandidates.set(false);
            return of([] as ChannelSearchCandidate[]);
          }
          this.searchingCandidates.set(true);
          return this.analytics.searchChannels(clean, this.lookupPlatform, 6);
        })
      )
      .subscribe((items) => {
        this.searchingCandidates.set(false);
        this.searchCandidates.set(items);
      });
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  onSearchInput(value: string): void {
    this.lookupError.set('');
    this.searchTerms$.next(value);
  }

  selectCandidate(cand: ChannelSearchCandidate): void {
    this.lookupQuery = cand.channelHandle || cand.externalChannelId || cand.channelName;
    this.searchCandidates.set([]);
    this.analyzeChannel();
  }

  analyzeChannel(): void {
    const q = this.lookupQuery.trim();
    if (!q) return;
    this.searchCandidates.set([]);
    this.analyzing.set(true);
    this.lookupError.set('');
    this.analytics.lookupChannel({
      query: q,
      platform: this.lookupPlatform,
      niche: this.lookupNiche.trim() || undefined
    }).subscribe({
      next: () => this.analyzing.set(false),
      error: (err) => {
        this.analyzing.set(false);
        this.lookupError.set(err?.error?.message || 'Unable to analyze this channel handle right now.');
      }
    });
  }

  saveAnalyzedChannel(): void {
    const active = this.lookupResult();
    if (!active || !this.auth.isAuthenticated()) return;
    this.savingChannel.set(true);
    this.lookupError.set('');
    this.analytics.autoAddMyChannel({
      query: active.channel.channelHandle || active.channel.externalChannelId || active.channel.channelName,
      platform: active.channel.platform,
      niche: active.channel.niche
    }).subscribe({
      next: (saved) => {
        this.savingChannel.set(false);
        this.channelSaved.emit(saved);
      },
      error: (err) => {
        this.savingChannel.set(false);
        if (err?.status === 401) {
          this.lookupError.set(
            err?.error?.message ||
            'Session could not be verified by the API (HTTP 401). Sign out and back in via SSO, or check that Jwt__SigningKey on social-analytics-api matches admin-api.'
          );
          return;
        }
        this.lookupError.set(err?.error?.message || 'Could not save channel to your workspace.');
      }
    });
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
