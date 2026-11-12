import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `
    <header class="main-header">
      <div class="header-container">
        <a routerLink="/" class="brand-link">
          <div class="brand-badge">⚡ SocialPulse</div>
          <span class="brand-sub">Keshav Analytics</span>
        </a>

        <nav class="nav-links">
          <a routerLink="/dashboard" routerLinkActive="active-nav">Dashboard</a>
          <a routerLink="/earnings" routerLinkActive="active-nav">💰 Earnings & RPM</a>
          <a routerLink="/advisor" routerLinkActive="active-nav">🚀 Growth Advisor</a>
          <a routerLink="/funnel" routerLinkActive="active-nav">🎯 Traffic Funnel</a>
          @if (auth.isAdmin()) {
            <a routerLink="/admin" routerLinkActive="active-nav" class="admin-nav">🛡️ Admin</a>
          }
        </nav>

        <div class="header-actions">
          @if (auth.isAuthenticated()) {
            <div class="user-chip">
              <span class="user-avatar">{{ auth.user()?.displayName?.charAt(0) || 'U' }}</span>
              <span class="user-name">{{ auth.user()?.displayName }}</span>
              <span class="user-role">{{ auth.user()?.roles?.[0] || 'User' }}</span>
              <button (click)="logout()" class="btn-logout" title="Sign out from SSO">Sign Out</button>
            </div>
          } @else {
            <button (click)="login()" class="btn-sso-login">
              <span>Sign in with SSO</span>
            </button>
          }
        </div>
      </div>
    </header>
  `,
  styles: [`
    .main-header {
      background: #ffffff;
      border-bottom: 1px solid #e2e8f0;
      position: sticky;
      top: 0;
      z-index: 50;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    .header-container {
      max-width: 1300px;
      margin: 0 auto;
      padding: 0.85rem 1rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1.5rem;
    }
    .brand-link {
      text-decoration: none;
      display: flex;
      flex-direction: column;
      gap: 0.1rem;
    }
    .brand-badge {
      font-size: 1.3rem;
      font-weight: 800;
      color: #0f172a;
      letter-spacing: -0.02em;
    }
    .brand-sub {
      font-size: 0.75rem;
      color: #64748b;
      font-weight: 500;
    }
    .nav-links {
      display: flex;
      align-items: center;
      gap: 1.25rem;
    }
    .nav-links a {
      text-decoration: none;
      color: #475569;
      font-weight: 600;
      font-size: 0.95rem;
      padding: 0.4rem 0.6rem;
      border-radius: 6px;
      transition: all 0.2s ease;
    }
    .nav-links a:hover {
      color: #2563eb;
      background: #eff6ff;
    }
    .nav-links a.active-nav {
      color: #2563eb;
      background: #dbeafe;
    }
    .nav-links a.admin-nav {
      border: 1px solid #cbd5e1;
      background: #f8fafc;
      color: #0f172a;
    }
    .nav-links a.admin-nav.active-nav {
      background: #0f172a;
      color: #ffffff;
      border-color: #0f172a;
    }
    .header-actions {
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .user-chip {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      background: #f1f5f9;
      padding: 0.35rem 0.75rem;
      border-radius: 9999px;
      border: 1px solid #e2e8f0;
    }
    .user-avatar {
      width: 26px;
      height: 26px;
      border-radius: 50%;
      background: #2563eb;
      color: #ffffff;
      font-size: 0.8rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .user-name {
      font-size: 0.85rem;
      font-weight: 600;
      color: #1e293b;
    }
    .user-role {
      font-size: 0.7rem;
      background: #e2e8f0;
      color: #475569;
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
      font-weight: 600;
    }
    .btn-logout {
      background: transparent;
      border: none;
      color: #ef4444;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
      padding: 0.2rem 0.4rem;
      border-radius: 4px;
    }
    .btn-logout:hover {
      background: #fee2e2;
    }
    .btn-sso-login {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.55rem 1.1rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.9rem;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-sso-login:hover {
      background: #1d4ed8;
    }
  `]
})
export class HeaderComponent {
  auth = inject(AuthService);

  login(): void {
    this.auth.loginRedirect();
  }

  logout(): void {
    this.auth.logout().subscribe();
  }
}
