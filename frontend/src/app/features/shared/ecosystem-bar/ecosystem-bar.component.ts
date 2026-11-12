import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EcosystemConfigService } from '../../../core/services/ecosystem-config.service';

@Component({
  selector: 'app-ecosystem-bar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <nav class="ecosystem-strip" aria-label="Keshav Singh Ecosystem">
      <div class="ecosystem-container">
        <span class="eco-label">Keshav Ecosystem:</span>
        <div class="eco-links">
          @for (site of sites(); track site.key) {
            <a [href]="site.url"
               [attr.target]="site.key === 'social' ? null : '_blank'"
               [attr.rel]="site.key === 'social' ? null : 'noopener'"
               class="eco-link"
               [class.active]="site.key === 'social'"
               [class.admin-pill]="site.key === 'admin'">
              <span>{{ site.icon || '🌐' }}</span>
              <span>{{ site.name }}</span>
            </a>
          }
        </div>
      </div>
    </nav>
  `,
  styles: [`
    .ecosystem-strip {
      background: #0f172a;
      color: #94a3b8;
      font-size: 0.8rem;
      border-bottom: 1px solid #1e293b;
      padding: 0.35rem 1rem;
    }
    .ecosystem-container {
      max-width: 1300px;
      margin: 0 auto;
      display: flex;
      align-items: center;
      gap: 0.8rem;
      flex-wrap: wrap;
    }
    .eco-label {
      font-weight: 600;
      color: #64748b;
      letter-spacing: 0.03em;
      text-transform: uppercase;
      font-size: 0.72rem;
    }
    .eco-links {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-wrap: wrap;
    }
    .eco-link {
      color: #cbd5e1;
      text-decoration: none;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      transition: all 0.15s ease;
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
    }
    .eco-link:hover {
      background: #1e293b;
      color: #38bdf8;
    }
    .eco-link.active {
      background: #2563eb;
      color: #ffffff;
      font-weight: 600;
    }
    .admin-pill {
      border: 1px solid #334155;
      color: #f1f5f9;
    }
    .admin-pill:hover {
      background: #475569;
      color: #f8fafc;
    }
  `]
})
export class EcosystemBarComponent {
  private ecosystem = inject(EcosystemConfigService);
  readonly sites = this.ecosystem.ecosystemBarSites;
}
