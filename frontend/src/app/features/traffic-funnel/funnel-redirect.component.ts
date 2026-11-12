import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AnalyticsService } from '../../core/services/analytics.service';

@Component({
  selector: 'app-funnel-redirect',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="redirect-wrap">
      <div class="redirect-card">
        @if (!error()) {
          <div class="icon">🚀</div>
          <h1>Redirecting to Keshav Ecosystem…</h1>
          <p>Recording funnel visit for <code>/t/{{ slug() }}</code></p>
        } @else {
          <div class="icon">⚠️</div>
          <h1>Tracking Link Not Found</h1>
          <p>{{ error() }}</p>
          <a routerLink="/funnel" class="back-btn">Back to Traffic Funnels</a>
        }
      </div>
    </div>
  `,
  styles: [`
    .redirect-wrap { min-height: 60vh; display: flex; align-items: center; justify-content: center; padding: 2rem; }
    .redirect-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; padding: 2rem; max-width: 440px; width: 100%; text-align: center; box-shadow: 0 4px 12px rgba(0,0,0,0.04); }
    .icon { font-size: 2.25rem; margin-bottom: 0.75rem; }
    h1 { font-size: 1.15rem; color: #0f172a; margin: 0 0 0.4rem; }
    p { font-size: 0.9rem; color: #64748b; margin: 0; }
    .back-btn { display: inline-block; margin-top: 1.25rem; padding: 0.55rem 1.1rem; background: #2563eb; color: #fff; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 0.9rem; }
  `]
})
export class FunnelRedirectComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private analytics = inject(AnalyticsService);

  slug = signal('');
  error = signal('');

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('slug') || '';
    this.slug.set(slug);
    if (!slug) {
      this.error.set('No tracking slug specified.');
      return;
    }

    this.analytics.trackClick(slug).subscribe({
      next: (res) => {
        if (res?.destination) {
          window.location.href = res.destination;
        } else {
          this.error.set('Campaign destination is missing.');
        }
      },
      error: () => {
        this.error.set('This tracking slug does not exist or is inactive.');
      }
    });
  }
}
