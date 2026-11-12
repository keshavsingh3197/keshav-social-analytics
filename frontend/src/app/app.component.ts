import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { EcosystemBarComponent } from './features/shared/ecosystem-bar/ecosystem-bar.component';
import { HeaderComponent } from './features/shared/header/header.component';
import { AuthService } from './core/services/auth.service';
import { EcosystemConfigService } from './core/services/ecosystem-config.service';

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
          <strong>SocialPulse by {{ creatorName() }}</strong>
          <p>Creator analytics, traffic attribution & algorithmic monetization intelligence.</p>
        </div>
        <div class="footer-links">
          @for (site of footerSites(); track site.key) {
            <a [href]="site.url" target="_blank" rel="noopener">{{ site.name }}</a>
          }
        </div>
      </div>
      <div class="footer-bottom">
        {{ copyrightNotice() }}
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
  private ecosystem = inject(EcosystemConfigService);

  readonly footerSites = this.ecosystem.footerSites;
  readonly copyrightNotice = this.ecosystem.copyrightNotice;
  readonly creatorName = this.ecosystem.creatorName;

  ngOnInit(): void {
    this.ecosystem.load();
    this.auth.ensureInitialized().subscribe();
  }
}
