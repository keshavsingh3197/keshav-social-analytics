import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalyticsService } from '../../core/services/analytics.service';
import { AdSlotComponent } from '../shared/ad-slot/ad-slot.component';
import { ChannelAuditResult, ChannelMetrics, SocialPlatform } from '../../core/models/analytics.models';

@Component({
  selector: 'app-channel-advisor',
  standalone: true,
  imports: [CommonModule, FormsModule, AdSlotComponent],
  template: `
    <div class="advisor-page">
      <div class="header-box">
        <h1>🚀 Channel Growth Advisor & AI Recommendations</h1>
        <p>
          Diagnose performance bottlenecks, optimize viewer retention curves, and discover algorithmic 
          recommendations computed from live channel metrics.
        </p>
      </div>

      <!-- Channel Search / Selection Bar -->
      <div class="search-bar-card">
        <div class="input-wrap">
          <label>Select Connected Channel or Enter Handle to Audit</label>
          <div class="input-action-row">
            @if (channels().length > 0) {
              <select (change)="selectConnectedChannel($event)" class="form-select">
                @for (ch of channels(); track ch.id) {
                  <option [value]="ch.id" [selected]="ch.channelHandle === selectedHandle()">
                    {{ ch.channelName }} ({{ ch.platform }})
                  </option>
                }
              </select>
            }
            <input 
              type="text" 
              [ngModel]="selectedHandle()" 
              (ngModelChange)="selectedHandle.set($event)"
              placeholder="@channelhandle" 
              class="form-input" />
            <select [ngModel]="selectedPlatform()" (ngModelChange)="selectedPlatform.set($event)" class="form-select">
              <option value="YouTube">YouTube</option>
              <option value="Instagram">Instagram</option>
              <option value="Facebook">Facebook</option>
              <option value="TikTok">TikTok</option>
              <option value="LinkedIn">LinkedIn</option>
            </select>
            <button (click)="runAudit()" class="btn-audit">Audit Channel</button>
          </div>
        </div>
      </div>

      @if (auditResult(); as audit) {
        <!-- Audit Score Overview -->
        <div class="audit-hero-card">
          <div class="score-circle">
            <span class="score-val">{{ audit.healthScoreOutOf100 }}</span>
            <span class="score-max">/ 100</span>
          </div>
          <div class="audit-summary-text">
            <h2>Channel Algorithmic Health: {{ healthLabel(audit.healthScoreOutOf100) }}</h2>
            <p>
              Audit for <strong>{{ audit.channelHandle }}</strong> computed against configured target engagement and watch-duration benchmarks.
            </p>
          </div>
        </div>

        <!-- Priority Suggestions Grid -->
        <div class="suggestions-section">
          <h2>🎯 Priority Growth Recommendations</h2>
          <div class="suggestions-grid">
            @for (sugg of audit.prioritySuggestions; track sugg.id) {
              <div class="sugg-card">
                <div class="sugg-header">
                  <span class="sugg-category">{{ sugg.category }}</span>
                  <span class="impact-badge" [attr.data-impact]="sugg.impact.toLowerCase()">
                    {{ sugg.impact }} Impact
                  </span>
                </div>
                <h3>{{ sugg.title }}</h3>
                <p class="sugg-desc">{{ sugg.description }}</p>
                
                <div class="action-box">
                  <strong>Action Required:</strong>
                  <span>{{ sugg.actionItem }}</span>
                </div>

                <div class="gain-chip">
                  Estimated Gain: <strong>{{ sugg.estimatedGain }}</strong>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Algorithmic Timing & Keywords Row -->
        <div class="meta-advisor-row">
          <div class="meta-card">
            <h3>⏰ Optimal Upload Time Windows</h3>
            <p class="meta-sub">Based on regional subscriber activity peaks:</p>
            <ul class="time-list">
              @for (time of audit.bestUploadTimes; track time) {
                <li>
                  <span class="clock-icon">🕒</span>
                  <span>{{ time }}</span>
                </li>
              }
            </ul>
          </div>

          <div class="meta-card">
            <h3>🔥 High-Demand SEO Keywords & Tags</h3>
            <p class="meta-sub">Currently trending tags to include in descriptions & titles:</p>
            <div class="tag-cloud">
              @for (kw of audit.highDemandKeywords; track kw) {
                <span class="kw-tag">#{{ kw }}</span>
              }
            </div>
          </div>
        </div>
      }

      <app-ad-slot placement="infeed" />
    </div>
  `,
  styles: [`
    .advisor-page {
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

    .search-bar-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
    }
    .input-wrap label {
      display: block;
      font-size: 0.85rem;
      font-weight: 600;
      color: #475569;
      margin-bottom: 0.4rem;
    }
    .input-action-row {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
    }
    .form-input {
      flex: 1;
      min-width: 200px;
      padding: 0.6rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 0.95rem;
    }
    .form-select {
      padding: 0.6rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 0.95rem;
    }
    .btn-audit {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.6rem 1.4rem;
      border-radius: 8px;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-audit:hover { background: #1d4ed8; }

    .audit-hero-card {
      background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
      color: #ffffff;
      padding: 1.75rem;
      border-radius: 14px;
      display: flex;
      align-items: center;
      gap: 1.75rem;
    }
    .score-circle {
      width: 90px;
      height: 90px;
      border-radius: 50%;
      background: #059669;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      color: #ffffff;
      flex-shrink: 0;
      box-shadow: 0 4px 12px rgba(5, 150, 105, 0.4);
    }
    .score-val { font-size: 1.6rem; font-weight: 800; line-height: 1; }
    .score-max { font-size: 0.75rem; opacity: 0.85; }
    .audit-summary-text h2 { margin: 0 0 0.4rem; font-size: 1.35rem; }
    .audit-summary-text p { margin: 0; color: #94a3b8; line-height: 1.5; font-size: 0.95rem; }

    .suggestions-section h2 {
      font-size: 1.35rem;
      color: #0f172a;
      margin: 0 0 1rem;
    }
    .suggestions-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: 1.25rem;
    }
    .sugg-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .sugg-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .sugg-category {
      font-size: 0.75rem;
      text-transform: uppercase;
      font-weight: 700;
      color: #64748b;
    }
    .impact-badge {
      font-size: 0.75rem;
      padding: 0.15rem 0.5rem;
      border-radius: 9999px;
      font-weight: 700;
    }
    .impact-badge[data-impact="high"] { background: #fee2e2; color: #dc2626; }
    .impact-badge[data-impact="medium"] { background: #fef3c7; color: #d97706; }
    .sugg-card h3 { margin: 0; font-size: 1.05rem; color: #1e293b; }
    .sugg-desc { margin: 0; font-size: 0.9rem; color: #475569; line-height: 1.5; }
    
    .action-box {
      background: #f8fafc;
      border-left: 3px solid #2563eb;
      padding: 0.6rem 0.8rem;
      font-size: 0.85rem;
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      color: #334155;
    }
    .gain-chip {
      margin-top: auto;
      font-size: 0.85rem;
      color: #059669;
      background: #ecfdf5;
      padding: 0.3rem 0.6rem;
      border-radius: 6px;
      align-self: flex-start;
    }

    .meta-advisor-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.25rem;
    }
    @media (max-width: 800px) {
      .meta-advisor-row { grid-template-columns: 1fr; }
    }
    .meta-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
    }
    .meta-card h3 { margin: 0 0 0.3rem; font-size: 1.1rem; color: #1e293b; }
    .meta-sub { margin: 0 0 0.85rem; font-size: 0.85rem; color: #64748b; }
    .time-list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .time-list li {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.9rem;
      color: #334155;
      font-weight: 500;
    }
    .tag-cloud {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .kw-tag {
      background: #eff6ff;
      color: #1d4ed8;
      padding: 0.35rem 0.7rem;
      border-radius: 9999px;
      font-size: 0.85rem;
      font-weight: 600;
    }
  `]
})
export class ChannelAdvisorComponent implements OnInit {
  private analytics = inject(AnalyticsService);

  readonly channels = signal<ChannelMetrics[]>([]);
  readonly selectedHandle = signal('');
  readonly selectedPlatform = signal<SocialPlatform>('YouTube');
  readonly auditResult = signal<ChannelAuditResult | null>(null);
  readonly auditing = signal(false);

  ngOnInit(): void {
    const active = this.analytics.activeLookup();
    if (active) {
      this.selectedHandle.set(active.channel.channelHandle || active.channel.channelName);
      this.selectedPlatform.set(active.channel.platform);
      this.auditResult.set(active.audit);
    }

    this.analytics.getChannels().subscribe(list => {
      this.channels.set(list);
      if (!active && list.length > 0) {
        this.selectedHandle.set(list[0].channelHandle);
        this.selectedPlatform.set(list[0].platform);
        this.runAudit();
      }
    });
  }

  selectConnectedChannel(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    const matched = this.channels().find(c => c.id === id);
    if (matched) {
      this.selectedHandle.set(matched.channelHandle);
      this.selectedPlatform.set(matched.platform);
      this.runAudit();
    }
  }

  healthLabel(score: number): string {
    if (score >= 85) return 'Excellent';
    if (score >= 70) return 'Strong';
    if (score >= 55) return 'Moderate';
    return 'Needs Attention';
  }

  runAudit(): void {
    const handle = this.selectedHandle().trim();
    const platform = this.selectedPlatform();
    if (!handle) return;

    this.auditing.set(true);
    const isSaved = this.channels().some(
      c => c.channelHandle.toLowerCase() === handle.toLowerCase() && c.platform === platform
    );
    if (isSaved) {
      this.analytics.auditChannel(handle, platform).subscribe({
        next: (res) => { this.auditResult.set(res); this.auditing.set(false); },
        error: () => this.auditing.set(false)
      });
      return;
    }

    this.analytics.lookupChannel({ query: handle, platform }).subscribe({
      next: (res) => { this.auditResult.set(res.audit); this.auditing.set(false); },
      error: () => {
        this.analytics.auditChannel(handle, platform).subscribe({
          next: (res) => { this.auditResult.set(res); this.auditing.set(false); },
          error: () => this.auditing.set(false)
        });
      }
    });
  }
}
