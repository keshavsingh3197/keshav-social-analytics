import { Component, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoadingService } from '../../../core/services/loading.service';

@Component({
  selector: 'app-global-loader',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (loading.active()) {
      <div class="global-progress" role="progressbar" aria-label="Loading analytics data">
        <div class="global-progress-track"></div>
      </div>
    }
    @if (loading.active() && loading.slow() && !dismissed()) {
      <div class="wakeup-toast" role="status" aria-live="polite">
        <span class="toast-spinner" aria-hidden="true"></span>
        <span>Waking up SocialPulse backend &amp; loading live metrics…</span>
        <button type="button" class="toast-close" aria-label="Dismiss" (click)="dismissed.set(true)">✕</button>
      </div>
    }
  `,
  styles: [`
    .global-progress {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      height: 3px;
      z-index: 9999;
      pointer-events: none;
      overflow: hidden;
      background: rgba(37, 99, 235, 0.16);
    }
    .global-progress-track {
      position: absolute;
      inset: 0 auto 0 0;
      width: 40%;
      background: linear-gradient(90deg, transparent, #2563eb, #38bdf8);
      animation: social-slide 1.15s ease-in-out infinite;
    }
    .wakeup-toast {
      position: fixed;
      bottom: 1rem;
      right: 1rem;
      z-index: 9998;
      display: inline-flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.65rem 0.95rem;
      border-radius: 12px;
      background: #0f172a;
      color: #f8fafc;
      border: 1px solid rgba(148, 163, 184, 0.3);
      box-shadow: 0 12px 30px rgba(15, 23, 42, 0.28);
      font-size: 0.83rem;
      font-weight: 500;
      max-width: min(92vw, 390px);
    }
    .toast-spinner {
      width: 16px;
      height: 16px;
      border-radius: 999px;
      border: 2.5px solid rgba(56, 189, 248, 0.25);
      border-top-color: #38bdf8;
      animation: social-spin 0.75s linear infinite;
      flex: none;
    }
    .toast-close {
      border: none;
      background: transparent;
      color: #94a3b8;
      cursor: pointer;
      font-size: 0.85rem;
      padding: 0 0.2rem;
    }
    .toast-close:hover { color: #ffffff; }
    @keyframes social-slide {
      0% { transform: translateX(-110%); }
      100% { transform: translateX(310%); }
    }
    @keyframes social-spin {
      to { transform: rotate(360deg); }
    }
    @media (prefers-reduced-motion: reduce) {
      .global-progress-track { animation-duration: 2.2s; }
      .toast-spinner { animation-duration: 1.5s; }
    }
  `],
})
export class GlobalLoaderComponent {
  readonly loading = inject(LoadingService);
  readonly dismissed = signal(false);

  constructor() {
    effect(() => {
      if (!this.loading.slow()) {
        this.dismissed.set(false);
      }
    });
  }
}
