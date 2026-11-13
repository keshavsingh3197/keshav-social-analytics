import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AnalyticsService } from '../../core/services/analytics.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-youtube-callback',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="callback-wrap">
      <div class="callback-card">
        @if (status() === 'working') {
          <div class="spinner"></div>
          <h2>Connecting your YouTube channel...</h2>
          <p>Exchanging secure OAuth tokens and syncing your channel analytics.</p>
        } @else if (status() === 'auth-required') {
          <div class="icon">🔐</div>
          <h2>Sign in to finish linking YouTube</h2>
          <p>Your OAuth authorization returned cleanly, but your session is signed out. Sign in with SSO and restart YouTube Connect.</p>
          <div class="actions">
            <button type="button" class="btn-primary" (click)="auth.loginRedirect()">Sign in with SSO</button>
            <a routerLink="/dashboard" class="btn-secondary">Back to Dashboard</a>
          </div>
        } @else if (status() === 'error') {
          <div class="icon">⚠️</div>
          <h2>YouTube Connection Failed</h2>
          <p class="err-msg">{{ message() }}</p>
          <div class="actions">
            <a routerLink="/dashboard" class="btn-primary">Return to Dashboard</a>
          </div>
        } @else {
          <div class="icon">✅</div>
          <h2>Connected {{ channelTitle() }}</h2>
          <p>{{ message() }}</p>
          <div class="actions">
            <a [routerLink]="returnPath()" class="btn-primary">Open Dashboard Now</a>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .callback-wrap {
      max-width: 540px;
      margin: 4rem auto;
      padding: 0 1.25rem;
    }
    .callback-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 18px;
      padding: 2.25rem 2rem;
      text-align: center;
      box-shadow: 0 10px 30px rgba(15, 23, 42, 0.06);
    }
    .spinner {
      width: 40px;
      height: 40px;
      margin: 0 auto 1rem;
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
    .icon { font-size: 2.2rem; margin-bottom: 0.75rem; }
    h2 { margin: 0 0 0.5rem; color: #0f172a; font-size: 1.35rem; }
    p { margin: 0 0 1.4rem; color: #475569; font-size: 0.92rem; line-height: 1.5; }
    .err-msg { color: #b91c1c; font-weight: 500; }
    .actions { display: flex; gap: 0.75rem; justify-content: center; flex-wrap: wrap; }
    .btn-primary, .btn-secondary {
      padding: 0.65rem 1.25rem;
      border-radius: 10px;
      font-weight: 700;
      font-size: 0.88rem;
      text-decoration: none;
      border: none;
      cursor: pointer;
    }
    .btn-primary { background: #2563eb; color: #ffffff; }
    .btn-secondary { background: #f1f5f9; color: #334155; }
  `]
})
export class YouTubeCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly analytics = inject(AnalyticsService);
  readonly auth = inject(AuthService);

  readonly status = signal<'working' | 'success' | 'error' | 'auth-required'>('working');
  readonly message = signal('');
  readonly channelTitle = signal('');
  readonly returnPath = signal('/dashboard');

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const error = params.get('error');
    const code = params.get('code');
    const state = params.get('state');

    if (error) {
      this.status.set('error');
      this.message.set(`Google returned error: ${error}`);
      return;
    }

    if (!code || !state) {
      this.status.set('error');
      this.message.set('Missing authorization code or OAuth state parameter.');
      return;
    }

    if (!this.auth.isAuthenticated()) {
      this.status.set('auth-required');
      return;
    }

    this.analytics.completeYouTubeOAuth(code, state).subscribe({
      next: (res) => {
        this.status.set('success');
        this.channelTitle.set(res.connection.channelTitle);
        this.message.set('Your YouTube metrics are synced to your private workspace.');
        const target = res.returnPath && res.returnPath.startsWith('/') ? res.returnPath : '/dashboard';
        this.returnPath.set(target);
        setTimeout(() => void this.router.navigateByUrl(target), 1200);
      },
      error: (err) => {
        this.status.set('error');
        this.message.set(err?.error?.message || 'Unable to complete YouTube OAuth exchange. Please try again.');
      }
    });
  }
}
