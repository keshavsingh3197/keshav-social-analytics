import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { EcosystemBarComponent } from './features/shared/ecosystem-bar/ecosystem-bar.component';
import { HeaderComponent } from './features/shared/header/header.component';
import { AuthService } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, EcosystemBarComponent, HeaderComponent],
  template: `
    <app-ecosystem-bar />
    <app-header />
    <main class="main-content">
      <router-outlet />
    </main>
    <footer class="app-footer">
      <div class="footer-container">
        <div class="footer-col">
          <strong>SocialPulse by Keshav Singh</strong>
          <p>Creator analytics, traffic attribution & algorithmic monetization intelligence.</p>
        </div>
        <div class="footer-links">
          <a href="https://studio.keshavsingh.in" target="_blank" rel="noopener">AnimStudio AI</a>
          <a href="https://panchang.keshavsingh.in" target="_blank" rel="noopener">Hindu Panchang</a>
          <a href="https://admin.keshavsingh.in" target="_blank" rel="noopener">Central SSO</a>
          <a href="https://git.keshavsingh.in" target="_blank" rel="noopener">Engineering Blog</a>
        </div>
      </div>
      <div class="footer-bottom">
        © 2026 Keshav Singh. Part of the keshavsingh.in ecosystem. All rights reserved.
      </div>
    </footer>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }
    .main-content {
      flex: 1;
    }
    .app-footer {
      background: #0f172a;
      color: #94a3b8;
      border-top: 1px solid #1e293b;
      padding: 2.5rem 1rem 1.5rem;
      margin-top: auto;
    }
    .footer-container {
      max-width: 1300px;
      margin: 0 auto;
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 1.5rem;
      padding-bottom: 1.5rem;
      border-bottom: 1px solid #1e293b;
    }
    .footer-col strong {
      color: #f8fafc;
      font-size: 1rem;
      display: block;
      margin-bottom: 0.25rem;
    }
    .footer-col p {
      margin: 0;
      font-size: 0.85rem;
    }
    .footer-links {
      display: flex;
      gap: 1.25rem;
      flex-wrap: wrap;
    }
    .footer-links a {
      color: #cbd5e1;
      text-decoration: none;
      font-size: 0.85rem;
      transition: color 0.15s;
    }
    .footer-links a:hover {
      color: #38bdf8;
    }
    .footer-bottom {
      max-width: 1300px;
      margin: 1.25rem auto 0;
      font-size: 0.8rem;
      color: #64748b;
      text-align: center;
    }
  `]
})
export class AppComponent implements OnInit {
  private auth = inject(AuthService);

  ngOnInit(): void {
    // Attempt silent SSO session resolution via .keshavsingh.in cookie
    this.auth.refresh().subscribe({
      next: (session) => {
        console.log('SSO session restored successfully for:', session.user.email);
      },
      error: () => {
        // Public visitors stay unauthenticated gracefully, allowing access to calculators and public stats
        console.log('No active SSO session found. Operating in public visitor mode.');
      }
    });
  }
}
