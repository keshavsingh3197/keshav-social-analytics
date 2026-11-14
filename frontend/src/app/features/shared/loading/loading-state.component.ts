import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SocialLoadingVariant = 'spinner' | 'cards' | 'rows' | 'kpis' | 'audit';

@Component({
  selector: 'app-loading-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (error) {
      <div class="error-card" role="alert">
        <span>⚠️ {{ error }}</span>
        @if (showRetry) {
          <button type="button" class="btn-retry" (click)="retry.emit()">🔄 Retry</button>
        }
      </div>
    } @else if (variant === 'kpis') {
      <div class="skel-kpis" role="status" aria-busy="true" [attr.aria-label]="label">
        @for (i of items(count); track i) {
          <div class="skel-card skel-kpi">
            <span class="skel-bar w-65 h-lg"></span>
            <span class="skel-bar w-80 h-xs"></span>
          </div>
        }
      </div>
    } @else if (variant === 'audit') {
      <div class="skel-wrap" role="status" aria-busy="true" [attr.aria-label]="label">
        <div class="loading-pill"><span class="mini-spin" aria-hidden="true"></span>{{ label }}</div>
        <div class="skel-card skel-audit-hero">
          <span class="skel-bar w-45 h-lg"></span>
          <span class="skel-bar w-75 h-sm"></span>
          <span class="skel-bar w-90 h-sm"></span>
        </div>
        <div class="skel-cards">
          @for (i of items(3); track i) {
            <div class="skel-card">
              <span class="skel-bar w-60 h-md"></span>
              <span class="skel-bar w-100 h-sm"></span>
              <span class="skel-bar w-85 h-sm"></span>
            </div>
          }
        </div>
      </div>
    } @else if (variant === 'cards') {
      <div class="skel-wrap" role="status" aria-busy="true" [attr.aria-label]="label">
        @if (label) {
          <div class="loading-pill"><span class="mini-spin" aria-hidden="true"></span>{{ label }}</div>
        }
        <div class="skel-cards">
          @for (i of items(count); track i) {
            <div class="skel-card">
              <span class="skel-bar w-70 h-md"></span>
              <span class="skel-bar w-100 h-sm"></span>
              <span class="skel-bar w-85 h-sm"></span>
              <span class="skel-bar w-45 h-xs"></span>
            </div>
          }
        </div>
      </div>
    } @else if (variant === 'rows') {
      <div class="skel-wrap" role="status" aria-busy="true" [attr.aria-label]="label">
        @if (label) {
          <div class="loading-pill"><span class="mini-spin" aria-hidden="true"></span>{{ label }}</div>
        }
        @for (i of items(count); track i) {
          <div class="skel-card skel-row">
            <span class="skel-bar w-35 h-sm"></span>
            <span class="skel-bar w-45 h-sm"></span>
            <span class="skel-bar w-15 h-sm"></span>
          </div>
        }
      </div>
    } @else {
      <div class="spinner-box" role="status" aria-busy="true">
        <span class="mini-spin lg" aria-hidden="true"></span>
        <span>{{ label }}</span>
      </div>
    }
  `,
  styles: [`
    :host { display: block; width: 100%; }
    .skel-wrap { display: flex; flex-direction: column; gap: 0.75rem; }
    .loading-pill {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      align-self: flex-start;
      padding: 0.38rem 0.8rem;
      border-radius: 999px;
      background: #eff6ff;
      border: 1px solid #bfdbfe;
      color: #1d4ed8;
      font-size: 0.82rem;
      font-weight: 600;
    }
    .mini-spin {
      width: 14px;
      height: 14px;
      border-radius: 999px;
      border: 2px solid rgba(37, 99, 235, 0.22);
      border-top-color: #2563eb;
      animation: s-spin 0.75s linear infinite;
      flex: none;
    }
    .mini-spin.lg { width: 26px; height: 26px; border-width: 3px; }
    .skel-kpis {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 0.75rem;
    }
    .skel-kpi {
      background: rgba(255, 255, 255, 0.08);
      border-color: rgba(255, 255, 255, 0.14);
    }
    .skel-cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 0.9rem;
    }
    .skel-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }
    .skel-audit-hero { padding: 1.4rem; }
    .skel-row {
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
    }
    .skel-bar {
      display: block;
      border-radius: 999px;
      background: linear-gradient(90deg, #e2e8f0 20%, #f1f5f9 50%, #e2e8f0 80%);
      background-size: 200% 100%;
      animation: s-shimmer 1.35s ease-in-out infinite;
    }
    .skel-kpi .skel-bar {
      background: linear-gradient(90deg, rgba(255,255,255,0.14) 20%, rgba(255,255,255,0.3) 50%, rgba(255,255,255,0.14) 80%);
      background-size: 200% 100%;
    }
    .h-xs { height: 10px; }
    .h-sm { height: 13px; }
    .h-md { height: 18px; }
    .h-lg { height: 26px; }
    .w-15 { width: 15%; }
    .w-35 { width: 35%; }
    .w-45 { width: 45%; }
    .w-60 { width: 60%; }
    .w-65 { width: 65%; }
    .w-70 { width: 70%; }
    .w-75 { width: 75%; }
    .w-80 { width: 80%; }
    .w-85 { width: 85%; }
    .w-90 { width: 90%; }
    .w-100 { width: 100%; }
    .spinner-box {
      background: #ffffff;
      border: 1px dashed #cbd5e1;
      border-radius: 12px;
      padding: 2rem 1rem;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.6rem;
      color: #475569;
      font-weight: 600;
    }
    .error-card {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
      border-radius: 12px;
      padding: 1rem 1.2rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      flex-wrap: wrap;
      font-weight: 600;
    }
    .btn-retry {
      background: #2563eb;
      color: #ffffff;
      border: none;
      border-radius: 8px;
      padding: 0.45rem 0.9rem;
      font-weight: 700;
      cursor: pointer;
    }
    @keyframes s-shimmer {
      0% { background-position: 200% 0; }
      100% { background-position: -200% 0; }
    }
    @keyframes s-spin {
      to { transform: rotate(360deg); }
    }
    @media (prefers-reduced-motion: reduce) {
      .skel-bar { animation: none; }
      .mini-spin { animation-duration: 1.5s; }
    }
  `],
})
export class LoadingStateComponent {
  @Input() variant: SocialLoadingVariant = 'cards';
  @Input() count = 3;
  @Input() label = 'Loading…';
  @Input() error: string | null = null;
  @Input() showRetry = true;
  @Output() retry = new EventEmitter<void>();

  items(n: number): number[] {
    return Array.from({ length: Math.min(12, Math.max(1, Math.trunc(n) || 1)) }, (_, i) => i);
  }
}
