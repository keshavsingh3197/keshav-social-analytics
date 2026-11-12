import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalyticsService } from '../../core/services/analytics.service';
import { EarningsCalculationRequest, EarningsReport, SocialPlatform } from '../../core/models/analytics.models';

@Component({
  selector: 'app-earnings-calculator',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="calculator-page">
      <div class="header-box">
        <h1>💰 Creator Earnings & RPM Calculator</h1>
        <p>
          Simulate revenue potential across YouTube, Instagram, and Facebook. Calculate AdSense RPM, 
          brand sponsorships, and website referral affiliate earnings based on actual traffic sessions.
        </p>
      </div>

      <div class="calculator-layout">
        <!-- Controls Panel -->
        <div class="controls-panel">
          <h3>Channel Parameters</h3>

          <div class="control-group">
            <label>Platform</label>
            <div class="platform-selector">
              @for (plat of platforms; track plat) {
                <button 
                  type="button"
                  [class.active]="request.platform === plat"
                  (click)="setPlatform(plat)"
                  class="plat-btn">
                  {{ plat }}
                </button>
              }
            </div>
          </div>

          <div class="control-group">
            <label>Content Niche</label>
            <select [(ngModel)]="request.niche" (change)="onNicheChange()" class="form-input">
              <option value="Tech & Dev">Tech & Dev (Higher RPM: \$4 - \$9)</option>
              <option value="AI & Animation">AI & Animation (RPM: \$3 - \$6)</option>
              <option value="Culture & Panchang">Culture & History (RPM: \$1.5 - \$3.5)</option>
              <option value="Finance & Investing">Finance & Investing (Highest RPM: \$8 - \$18)</option>
              <option value="Entertainment & Vlogs">Entertainment & Vlogs (RPM: \$1.2 - \$2.5)</option>
            </select>
          </div>

          <div class="control-group">
            <div class="label-row">
              <label>Monthly Traffic / Views</label>
              <span class="val-highlight">{{ request.monthlyViews | number }} views</span>
            </div>
            <input 
              type="range" 
              min="10000" 
              max="2000000" 
              step="10000" 
              [(ngModel)]="request.monthlyViews" 
              (input)="recalculate()"
              class="range-slider" />
          </div>

          <div class="control-group">
            <div class="label-row">
              <label>Estimated RPM (Ad Revenue per 1,000 views)</label>
              <span class="val-highlight">\${{ request.estimatedRpm }}</span>
            </div>
            <input 
              type="range" 
              min="0.5" 
              max="15.0" 
              step="0.25" 
              [(ngModel)]="request.estimatedRpm" 
              (input)="recalculate()"
              class="range-slider" />
          </div>

          <div class="control-group">
            <div class="label-row">
              <label>Monthly Sponsorship Deals</label>
              <span class="val-highlight">{{ request.monthlySponsorshipDeals }} deal(s)</span>
            </div>
            <input 
              type="range" 
              min="0" 
              max="6" 
              step="1" 
              [(ngModel)]="request.monthlySponsorshipDeals" 
              (input)="recalculate()"
              class="range-slider" />
          </div>

          <div class="control-group">
            <label>Avg Sponsorship Deal Payout ($)</label>
            <input 
              type="number" 
              [(ngModel)]="request.avgSponsorshipRate" 
              (input)="recalculate()" 
              class="form-input" />
          </div>

          <div class="control-group">
            <div class="label-row">
              <label>Affiliate / Sibling Website Conversion (%)</label>
              <span class="val-highlight">{{ request.affiliateConversionRatePercentage }}%</span>
            </div>
            <input 
              type="range" 
              min="0.1" 
              max="2.5" 
              step="0.1" 
              [(ngModel)]="request.affiliateConversionRatePercentage" 
              (input)="recalculate()"
              class="range-slider" />
          </div>
        </div>

        <!-- Live Projection Result Panel -->
        <div class="results-panel">
          @if (report) {
            <div class="summary-hero">
              <span class="hero-lbl">Total Estimated Monthly Income</span>
              <div class="hero-amount">\${{ report.totalEstimatedMonthlyRevenue | number }}</div>
              <span class="hero-sub">(\${{ report.totalEstimatedYearlyRevenue | number }} projected annually)</span>
              <div class="effective-rpm-chip">
                Effective RPM across all streams: <strong>\${{ report.effectiveRpm }}</strong> / 1k views
              </div>
            </div>

            <div class="streams-breakdown">
              <h3>Revenue Streams Distribution</h3>
              <div class="stream-bars">
                @for (stream of report.streams; track stream.streamName) {
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
                @for (tip of report.monetizationOpportunities; track tip) {
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
  `]
})
export class EarningsCalculatorComponent implements OnInit {
  private analytics = inject(AnalyticsService);

  platforms: SocialPlatform[] = ['YouTube', 'Instagram', 'Facebook', 'TikTok'];
  request: EarningsCalculationRequest = {
    platform: 'YouTube',
    niche: 'Tech & Dev',
    monthlyViews: 250000,
    estimatedRpm: 4.5,
    monthlySponsorshipDeals: 2,
    avgSponsorshipRate: 650,
    affiliateConversionRatePercentage: 0.6,
    avgAffiliatePayout: 18
  };

  report: EarningsReport | null = null;

  ngOnInit(): void {
    this.recalculate();
  }

  setPlatform(plat: SocialPlatform): void {
    this.request.platform = plat;
    if (plat === 'YouTube') this.request.estimatedRpm = 4.5;
    if (plat === 'Instagram') this.request.estimatedRpm = 1.8;
    if (plat === 'Facebook') this.request.estimatedRpm = 2.2;
    if (plat === 'TikTok') this.request.estimatedRpm = 1.0;
    this.recalculate();
  }

  onNicheChange(): void {
    if (this.request.niche.includes('Finance')) this.request.estimatedRpm = 9.5;
    else if (this.request.niche.includes('Tech')) this.request.estimatedRpm = 5.0;
    else if (this.request.niche.includes('Culture')) this.request.estimatedRpm = 2.5;
    else this.request.estimatedRpm = 2.0;
    this.recalculate();
  }

  recalculate(): void {
    this.analytics.calculateEarnings(this.request).subscribe(rep => this.report = rep);
  }
}
