import { CommonModule } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MonetizationComplianceAudit, PolicyChecklistItem } from '../../../core/models/analytics.models';

type VerdictFilter = 'all' | 'wrong' | 'good' | 'verify';

const POLICY_URL = 'https://support.google.com/youtube/answer/1311392?hl=en';

@Component({
  selector: 'app-monetization-policy-audit',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    @if (audit(); as a) {
      <section class="policy-card" aria-labelledby="policy-audit-title">
        <div class="policy-head">
          <div>
            <span class="policy-kicker">YouTube Channel Monetization &amp; Recommendation Compliance</span>
            <h3 id="policy-audit-title">🛡️ Monetization &amp; Originality Policy Audit</h3>
            <p class="policy-summary">{{ a.summaryNote }}</p>
          </div>
          <div class="policy-actions">
            <span class="policy-score" [attr.data-status]="a.overallStatus.toLowerCase()">
              {{ a.scoreOutOf100 }}/100 · {{ statusLabel(a.overallStatus) }}
            </span>
            <a [href]="a.policyUrl || defaultPolicyUrl" target="_blank" rel="noopener noreferrer" class="policy-btn primary">
              Official YPP Rules (answer/1311392) ↗
            </a>
            <a href="https://studio.youtube.com" target="_blank" rel="noopener noreferrer" class="policy-btn">
              YouTube Studio ↗
            </a>
            @if (fullAdvisorHandle()) {
              <a routerLink="/advisor" [queryParams]="{ handle: fullAdvisorHandle(), platform: 'YouTube' }" class="policy-btn">
                Open Advisor Studio →
              </a>
            }
          </div>
        </div>

        <div class="policy-tabs" role="tablist" aria-label="Filter policy checks">
          <button type="button" role="tab" [class.active]="filter() === 'all'" (click)="filter.set('all')">
            All checks ({{ a.items.length }})
          </button>
          <button type="button" role="tab" [class.active]="filter() === 'wrong'" (click)="filter.set('wrong')">
            ⚠️ Needs Fixing ({{ counts().wrong }})
          </button>
          <button type="button" role="tab" [class.active]="filter() === 'good'" (click)="filter.set('good')">
            ✅ Doing Good ({{ counts().good }})
          </button>
          <button type="button" role="tab" [class.active]="filter() === 'verify'" (click)="filter.set('verify')">
            📱 Verify Mobile / Studio ({{ counts().verify }})
          </button>
        </div>

        <div class="policy-grid">
          @for (item of visibleItems(); track item.id) {
            <article class="policy-item" [attr.data-verdict]="item.verdict">
              <div class="policy-item-top">
                <span class="verdict-pill" [attr.data-verdict]="item.verdict">
                  {{ verdictIcon(item.verdict) }} {{ verdictLabel(item.verdict) }}
                </span>
                <span class="policy-cat">{{ item.category }}</span>
              </div>
              <h4>{{ item.title }}</h4>
              <p class="policy-detail">{{ item.detail }}</p>
              @if (item.steps.length > 0) {
                <ol class="policy-steps">
                  @for (step of item.steps; track $index) {
                    <li>{{ step }}</li>
                  }
                </ol>
              }
            </article>
          } @empty {
            <p class="policy-empty">No items in this filter.</p>
          }
        </div>
      </section>
    }
  `,
  styles: [`
    .policy-card {
      background: #fff;
      border: 1px solid #cbd5e1;
      border-radius: 14px;
      padding: 1.15rem 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.9rem;
    }
    .policy-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; flex-wrap: wrap; }
    .policy-kicker { display: block; font-size: 0.72rem; font-weight: 700; text-transform: uppercase; letter-spacing: .04em; color: #2563eb; margin-bottom: 0.2rem; }
    .policy-head h3 { margin: 0 0 0.25rem; font-size: 1.1rem; color: #0f172a; }
    .policy-summary { margin: 0; font-size: 0.85rem; color: #475569; line-height: 1.45; max-width: 720px; }
    .policy-actions { display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center; }
    .policy-score {
      font-size: 0.78rem; font-weight: 800; padding: 0.35rem 0.75rem; border-radius: 999px;
      background: #ecfdf5; color: #047857; border: 1px solid #a7f3d0;
    }
    .policy-score[data-status="needsattention"] { background: #fffbeb; color: #b45309; border-color: #fde68a; }
    .policy-score[data-status="atrisk"] { background: #fef2f2; color: #b91c1c; border-color: #fecaca; }
    .policy-btn {
      font-size: 0.78rem; font-weight: 700; padding: 0.4rem 0.75rem; border-radius: 8px; text-decoration: none;
      border: 1px solid #cbd5e1; background: #fff; color: #1e293b;
    }
    .policy-btn.primary { background: #eff6ff; border-color: #93c5fd; color: #1d4ed8; }
    .policy-tabs { display: flex; flex-wrap: wrap; gap: 0.45rem; }
    .policy-tabs button {
      font-size: 0.78rem; font-weight: 600; padding: 0.35rem 0.75rem; border-radius: 999px; cursor: pointer;
      border: 1px solid #cbd5e1; background: #f8fafc; color: #334155;
    }
    .policy-tabs button.active { background: #0f172a; border-color: #0f172a; color: #fff; }
    .policy-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(285px, 1fr)); gap: 0.85rem; }
    .policy-item {
      border: 1px solid #e2e8f0; border-left-width: 4px; border-radius: 10px;
      padding: 0.9rem 1rem; background: #f8fafc; display: flex; flex-direction: column; gap: 0.4rem;
    }
    .policy-item[data-verdict="good"] { border-left-color: #10b981; }
    .policy-item[data-verdict="wrong"] { border-left-color: #ef4444; background: #fffafa; }
    .policy-item[data-verdict="verify"] { border-left-color: #0284c7; background: #f8fbff; }
    .policy-item-top { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .verdict-pill { font-size: 0.7rem; font-weight: 800; padding: 0.16rem 0.5rem; border-radius: 999px; }
    .verdict-pill[data-verdict="good"] { background: #dcfce7; color: #166534; }
    .verdict-pill[data-verdict="wrong"] { background: #fee2e2; color: #991b1b; }
    .verdict-pill[data-verdict="verify"] { background: #e0f2fe; color: #075985; }
    .policy-cat { font-size: 0.72rem; font-weight: 600; color: #64748b; }
    .policy-item h4 { margin: 0; font-size: 0.92rem; color: #0f172a; }
    .policy-detail { margin: 0; font-size: 0.81rem; color: #475569; line-height: 1.45; }
    .policy-steps { margin: 0.2rem 0 0; padding-left: 1.15rem; font-size: 0.79rem; color: #334155; display: flex; flex-direction: column; gap: 0.25rem; line-height: 1.4; }
    .policy-empty { margin: 0.25rem 0; font-size: 0.84rem; color: #64748b; }
  `],
})
export class MonetizationPolicyAuditComponent {
  readonly audit = input<MonetizationComplianceAudit | null | undefined>(null);
  readonly fullAdvisorHandle = input<string | null | undefined>(null);

  readonly defaultPolicyUrl = POLICY_URL;
  readonly filter = signal<VerdictFilter>('all');

  readonly counts = computed(() => {
    const items = this.audit()?.items ?? [];
    return {
      good: items.filter((i) => i.verdict === 'good').length,
      wrong: items.filter((i) => i.verdict === 'wrong').length,
      verify: items.filter((i) => i.verdict === 'verify').length,
    };
  });

  readonly visibleItems = computed<readonly PolicyChecklistItem[]>(() => {
    const items = this.audit()?.items ?? [];
    const f = this.filter();
    if (f === 'all') return items;
    return items.filter((i) => i.verdict === f);
  });

  statusLabel(status: string): string {
    switch (status) {
      case 'Strong': return 'Policy Strong';
      case 'AtRisk': return 'Action Required';
      default: return 'Needs Attention';
    }
  }

  verdictIcon(v: string): string {
    switch (v) {
      case 'good': return '✅';
      case 'wrong': return '⚠️';
      default: return '📱';
    }
  }

  verdictLabel(v: string): string {
    switch (v) {
      case 'good': return 'Doing Good';
      case 'wrong': return 'Needs Fixing';
      default: return 'Verify Manually';
    }
  }
}
