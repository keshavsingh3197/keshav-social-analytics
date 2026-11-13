import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalyticsService } from '../../core/services/analytics.service';
import {
  EarningsCalculationRequest,
  EarningsConfig,
  EarningsReport,
  SocialPlatform
} from '../../core/models/analytics.models';

@Component({
  selector: 'app-earnings-calculator',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="calculator-page">
      <div class="header-box">
        <h1>💰 Creator Earnings & RPM Calculator</h1>
        <p>
          Simulate revenue potential across connected channels. Calculate AdSense RPM,
          brand sponsorships, and website referral affiliate earnings using real view metrics or custom targets.
        </p>
      </div>

      @if (channels().length > 0) {
        <div class="live-preset-bar">
          <span class="preset-lbl">⚡ Load Live Channel Metrics:</span>
          @for (ch of channels(); track ch.id) {
            <button type="button" class="live-ch-btn" (click)="applyChannelPreset(ch)">
              <span>▶ {{ ch.channelName }}</span>
              <small>{{ ch.channelHandle }} • {{ ch.totalViewsLast30Days | number }} views</small>
            </button>
          }
        </div>
      }

      <div class="calculator-layout">
        <!-- Controls Panel -->
        <div class="controls-panel">
          <h3>Channel Parameters</h3>

          <div class="control-group">
            <label>Platform</label>
            <div class="platform-selector">
              @for (bench of benchmarks(); track bench.platform) {
                <button 
                  type="button"
                  [class.active]="request().platform === bench.platform"
                  (click)="setPlatform(bench.platform)"
                  class="plat-btn">
                  {{ bench.icon }} {{ bench.platform }}
                </button>
              }
            </div>
          </div>

          <div class="control-group">
            <label>Content Niche</label>
            <select [ngModel]="request().niche" (ngModelChange)="onNicheChange($event)" class="form-input">
              @for (niche of niches(); track niche.id) {
                <option [value]="niche.name">
                  {{ niche.name }} (RPM: \${{ niche.minRpmUsd }} - \${{ niche.maxRpmUsd }})
                </option>
              }
            </select>
          </div>

          <div class="control-group">
            <div class="label-row">
              <label>Monthly Traffic / Views</label>
              <span class="val-highlight">{{ request().monthlyViews | number }} views</span>
            </div>
            <input 
              type="range" 
              min="10000" 
              max="2000000" 
              step="10000" 
              [ngModel]="request().monthlyViews" 
              (ngModelChange)="updateNumericField('monthlyViews', $event)"
              class="range-slider" />
          </div>

          <div class="control-group">
            <div class="label-row">
              <label>Estimated RPM (Ad Revenue per 1,000 views)</label>
              <span class="val-highlight">\${{ request().estimatedRpm }}</span>
            </div>
            <input 
              type="range" 
              min="0.5" 
              max="15.0" 
              step="0.25" 
              [ngModel]="request().estimatedRpm" 
              (ngModelChange)="updateNumericField('estimatedRpm', $event)"
              class="range-slider" />
          </div>

          <div class="control-group">
            <div class="label-row">
              <label>Monthly Sponsorship Deals</label>
              <span class="val-highlight">{{ request().monthlySponsorshipDeals }} deal(s)</span>
            </div>
            <input 
              type="range" 
              min="0" 
              max="6" 
              step="1" 
              [ngModel]="request().monthlySponsorshipDeals" 
              (ngModelChange)="updateNumericField('monthlySponsorshipDeals', $event)"
              class="range-slider" />
          </div>

          <div class="control-group">
            <label>Avg Sponsorship Deal Payout ($)</label>
            <input 
              type="number" 
              [ngModel]="request().avgSponsorshipRate" 
              (ngModelChange)="updateNumericField('avgSponsorshipRate', $event)" 
              class="form-input" />
          </div>

          <div class="control-group">
            <div class="label-row">
              <label>Affiliate / Sibling Website Conversion (%)</label>
              <span class="val-highlight">{{ request().affiliateConversionRatePercentage }}%</span>
            </div>
            <input 
              type="range" 
              min="0.1" 
              max="2.5" 
              step="0.1" 
              [ngModel]="request().affiliateConversionRatePercentage" 
              (ngModelChange)="updateNumericField('affiliateConversionRatePercentage', $event)"
              class="range-slider" />
          </div>
        </div>

        <!-- Live Projection Result Panel -->
        <div class="results-panel">
          @if (report(); as rep) {
            <div class="summary-hero">
              <span class="hero-lbl">Total Estimated Monthly Income</span>
              <div class="hero-amount">\${{ rep.totalEstimatedMonthlyRevenue | number }}</div>
              <span class="hero-sub">(\${{ rep.totalEstimatedYearlyRevenue | number }} projected annually)</span>
              <div class="effective-rpm-chip">
                Effective RPM across all streams: <strong>\${{ rep.effectiveRpm }}</strong> / 1k views
              </div>
            </div>

            <div class="streams-breakdown">
              <h3>Revenue Streams Distribution</h3>
              <div class="stream-bars">
                @for (stream of rep.streams; track stream.streamName) {
                  <div class="stream-item">
                    <div class="st-info">
                      <span class="st-name">{{ stream.streamName }}</span>
                      <span class="st-amt">\${{ stream.amount | number }} ({{ stream.percentageShare }}%)</span>
                    </div>
                    <div class="bar-bg">
                      <div class="bar-fill" [style.width.%]="stream.percentageShare"></div>
                    </div>
                  </div>
                }
              </div>
            </div>

            <div class="monetization-tips">
              <h3>💡 High-ROI Monetization Actions</h3>
              <ul>
                @for (tip of rep.monetizationOpportunities; track tip) {
                  <li>{{ tip }}</li>
                }
              </ul>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .calculator-page {
      max-width: 1300px;
      margin: 0 auto;
      padding: 1.5rem 1rem 3rem;
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
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
      max-width: 800px;
      line-height: 1.5;
    }
    .calculator-layout {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.5rem;
    }
    @media (max-width: 900px) {
      .calculator-layout { grid-template-columns: 1fr; }
    }
    .controls-panel {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      padding: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.02);
    }
    .controls-panel h3 {
      margin: 0;
      font-size: 1.2rem;
      color: #1e293b;
    }
    .control-group {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
    }
    .control-group label {
      font-size: 0.85rem;
      font-weight: 600;
      color: #475569;
    }
    .label-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .val-highlight {
      font-weight: 700;
      color: #2563eb;
      font-size: 0.9rem;
    }
    .platform-selector {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .plat-btn {
      padding: 0.4rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      background: #f8fafc;
      cursor: pointer;
      font-size: 0.85rem;
      font-weight: 600;
      transition: all 0.15s ease;
    }
    .plat-btn.active {
      background: #2563eb;
      color: white;
      border-color: #2563eb;
    }
    .form-input {
      padding: 0.6rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
    }
    .range-slider {
      width: 100%;
      accent-color: #2563eb;
      cursor: pointer;
    }

    .results-panel {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }
    .summary-hero {
      background: linear-gradient(135deg, #059669 0%, #065f46 100%);
      color: #ffffff;
      padding: 1.75rem;
      border-radius: 14px;
      box-shadow: 0 4px 15px rgba(5, 150, 105, 0.2);
      text-align: center;
    }
    .hero-lbl {
      font-size: 0.9rem;
      opacity: 0.9;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .hero-amount {
      font-size: 2.8rem;
      font-weight: 800;
      margin: 0.4rem 0;
    }
    .hero-sub {
      font-size: 0.95rem;
      opacity: 0.9;
    }
    .effective-rpm-chip {
      margin-top: 1rem;
      background: rgba(255, 255, 255, 0.15);
      display: inline-block;
      padding: 0.4rem 0.9rem;
      border-radius: 9999px;
      font-size: 0.85rem;
    }

    .streams-breakdown {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      padding: 1.25rem;
    }
    .streams-breakdown h3 {
      margin: 0 0 1rem;
      font-size: 1.1rem;
      color: #1e293b;
    }
    .stream-bars {
      display: flex;
      flex-direction: column;
      gap: 0.8rem;
    }
    .st-info {
      display: flex;
      justify-content: space-between;
      font-size: 0.85rem;
      font-weight: 600;
      color: #334155;
      margin-bottom: 0.25rem;
    }
    .bar-bg {
      background: #f1f5f9;
      height: 10px;
      border-radius: 9999px;
      overflow: hidden;
    }
    .bar-fill {
      background: #2563eb;
      height: 100%;
      border-radius: 9999px;
      transition: width 0.3s ease;
    }

    .monetization-tips {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      padding: 1.25rem;
    }
    .monetization-tips h3 {
      margin: 0 0 0.75rem;
      font-size: 1.05rem;
      color: #1e293b;
    }
    .monetization-tips ul {
      margin: 0;
      padding-left: 1.2rem;
      color: #475569;
      font-size: 0.9rem;
      line-height: 1.6;
    }
    .live-preset-bar {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      flex-wrap: wrap;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 0.75rem 1rem;
    }
    .preset-lbl {
      font-size: 0.82rem;
      font-weight: 700;
      color: #475569;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .live-ch-btn {
      display: inline-flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.1rem;
      padding: 0.45rem 0.85rem;
      border-radius: 8px;
      background: #ffffff;
      border: 1px solid #cbd5e1;
      cursor: pointer;
      color: #0f172a;
      font-weight: 600;
      font-size: 0.85rem;
      transition: all 0.15s ease;
    }
    .live-ch-btn small { color: #64748b; font-weight: 500; font-size: 0.74rem; }
    .live-ch-btn:hover { border-color: #2563eb; background: #eff6ff; color: #1d4ed8; }
  `]
})
export class EarningsCalculatorComponent implements OnInit {
  private analytics = inject(AnalyticsService);

  readonly config = signal<EarningsConfig | null>(null);
  readonly channels = signal<import('../../core/models/analytics.models').ChannelMetrics[]>([]);
  readonly benchmarks = computed(() => this.config()?.platformBenchmarks ?? []);
  readonly niches = computed(() => this.config()?.niches ?? []);

  readonly request = signal<EarningsCalculationRequest>({
    platform: 'YouTube',
    niche: '',
    monthlyViews: 100000,
    estimatedRpm: 3.0,
    monthlySponsorshipDeals: 1,
    avgSponsorshipRate: 250,
    affiliateConversionRatePercentage: 0.5,
    avgAffiliatePayout: 15
  });

  readonly report = signal<EarningsReport | null>(null);

  ngOnInit(): void {
    this.analytics.getChannels().subscribe(chs => this.channels.set(chs));
    this.analytics.getEarningsConfig().subscribe(cfg => {
      this.config.set(cfg);
      const firstBench = cfg.platformBenchmarks[0];
      const firstNiche = cfg.niches[0];
      if (firstBench) {
        this.request.update(r => ({
          ...r,
          platform: (firstBench.platform as SocialPlatform) || 'YouTube',
          niche: firstNiche?.name || r.niche,
          monthlyViews: firstBench.defaultMonthlyViews,
          estimatedRpm: firstBench.defaultRpmUsd,
          avgSponsorshipRate: firstBench.defaultSponsorshipUsd,
          affiliateConversionRatePercentage: firstBench.affiliateConversionRatePercent,
          avgAffiliatePayout: firstBench.avgOrderValueUsd
        }));
      }
      this.recalculate();
    });
  }

  applyChannelPreset(ch: import('../../core/models/analytics.models').ChannelMetrics): void {
    this.request.update(r => ({
      ...r,
      platform: ch.platform,
      monthlyViews: Math.max(ch.totalViewsLast30Days, 1000),
      estimatedRpm: ch.rpmUsd && ch.rpmUsd > 0 ? ch.rpmUsd : r.estimatedRpm
    }));
    this.recalculate();
  }

  setPlatform(plat: string): void {
    const bench = this.benchmarks().find(b => b.platform.toLowerCase() === plat.toLowerCase());
    this.request.update(r => ({
      ...r,
      platform: plat as SocialPlatform,
      monthlyViews: bench?.defaultMonthlyViews ?? r.monthlyViews,
      estimatedRpm: bench?.defaultRpmUsd ?? r.estimatedRpm,
      avgSponsorshipRate: bench?.defaultSponsorshipUsd ?? r.avgSponsorshipRate,
      affiliateConversionRatePercentage: bench?.affiliateConversionRatePercent ?? r.affiliateConversionRatePercentage,
      avgAffiliatePayout: bench?.avgOrderValueUsd ?? r.avgAffiliatePayout
    }));
    this.recalculate();
  }

  onNicheChange(nicheName: string): void {
    const niche = this.niches().find(n => n.name === nicheName);
    this.request.update(r => ({
      ...r,
      niche: nicheName,
      estimatedRpm: niche?.suggestedRpmUsd ?? r.estimatedRpm
    }));
    this.recalculate();
  }

  updateNumericField(
    key: 'monthlyViews' | 'estimatedRpm' | 'monthlySponsorshipDeals' | 'avgSponsorshipRate' | 'affiliateConversionRatePercentage',
    rawValue: unknown
  ): void {
    const val = Number(rawValue);
    if (!Number.isFinite(val)) return;
    this.request.update(r => ({ ...r, [key]: val }));
    this.recalculate();
  }

  recalculate(): void {
    this.analytics.calculateEarnings(this.request()).subscribe(rep => this.report.set(rep));
  }
}
