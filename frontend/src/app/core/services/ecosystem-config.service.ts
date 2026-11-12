import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppConfigEnvelope, EcosystemWebsite } from '../models/analytics.models';

const FALLBACK_WEBSITES: EcosystemWebsite[] = [
  { key: 'studio', name: 'Studio AI', url: 'https://studio.keshavsingh.in', icon: '🎬', description: 'Generate 2D/3D AI animated stories & YouTube Shorts in minutes.', sortOrder: 10, showInEcosystemBar: true },
  { key: 'social', name: 'Social Analytics', url: 'https://social.keshavsingh.in', icon: '📊', description: 'Track creator analytics, RPM, growth audits & link funnels.', sortOrder: 20, showInEcosystemBar: true },
  { key: 'panchang', name: 'Hindu Panchang', url: 'https://panchang.keshavsingh.in', icon: '🕉️', description: 'Daily Vedic Panchang, Choghadiya, festivals & spiritual video feeds.', sortOrder: 30, showInEcosystemBar: true },
  { key: 'ghar-ledger', name: 'Ghar Ledger', url: 'https://ghar.keshavsingh.in', icon: '💰', description: 'Smart family & personal finance ledger.', sortOrder: 40, showInEcosystemBar: true },
  { key: 'pratiyogita', name: 'Pratiyogita', url: 'https://pratiyogita.keshavsingh.in', icon: '🏆', description: 'Competitive exam preparation & quizzes.', sortOrder: 50, showInEcosystemBar: true },
  { key: 'blog', name: 'Content Blog', url: 'https://git.keshavsingh.in', icon: '✍️', description: 'Technical guides, architecture & engineering notes.', sortOrder: 60, showInEcosystemBar: true },
  { key: 'portfolio', name: 'Portfolio', url: 'https://keshavsingh.in', icon: '🌐', description: 'Keshav Singh — Software Architect & Creator Hub.', sortOrder: 70, showInEcosystemBar: false },
  { key: 'admin', name: 'Admin & SSO', url: 'https://admin.keshavsingh.in', icon: '🛡️', description: 'Central identity provider & global ecosystem configuration.', sortOrder: 90, showInEcosystemBar: true }
];

@Injectable({ providedIn: 'root' })
export class EcosystemConfigService {
  private http = inject(HttpClient);
  private loadOnce$?: ReturnType<HttpClient['get']>;

  readonly websites = signal<EcosystemWebsite[]>(FALLBACK_WEBSITES);
  readonly values = signal<Record<string, string>>({});
  readonly loadedFromCentral = signal(false);

  readonly ecosystemBarSites = computed(() =>
    this.websites()
      .filter(w => w.showInEcosystemBar !== false)
      .sort((a, b) => a.sortOrder - b.sortOrder)
  );

  readonly promoSiblingSites = computed(() =>
    this.websites()
      .filter(w => w.key !== 'social' && w.key !== 'admin' && w.key !== 'blog-admin')
      .sort((a, b) => a.sortOrder - b.sortOrder)
  );

  readonly footerSites = computed(() =>
    this.websites()
      .filter(w => ['studio', 'panchang', 'blog', 'admin', 'portfolio'].includes(w.key))
      .sort((a, b) => a.sortOrder - b.sortOrder)
  );

  readonly creatorName = computed(() =>
    this.values()['ecosystem.creatorName'] || 'Keshav Singh'
  );

  readonly copyrightNotice = computed(() =>
    this.values()['ecosystem.copyrightNotice'] ||
    '© 2026 Keshav Singh. Part of the keshavsingh.in ecosystem. All rights reserved.'
  );

  readonly youtubeChannelName = computed(() =>
    this.values()['ecosystem.youtubeChannelName'] || 'Keshav Singh Official'
  );

  readonly youtubeChannelUrl = computed(() =>
    this.values()['ecosystem.youtubeChannelUrl'] || 'https://www.youtube.com/@KeshavSingh'
  );

  readonly featuredPromoText = computed(() =>
    this.values()['social.featuredPromoText'] || ''
  );

  readonly adminPortalUrl = computed(() =>
    this.websites().find(w => w.key === 'admin')?.url || 'https://admin.keshavsingh.in'
  );

  load(): void {
    if (!this.loadOnce$) {
      const configUrl = `${environment.idpUrl}/config`;
      this.loadOnce$ = this.http.get<AppConfigEnvelope>(configUrl).pipe(
        tap(envelope => {
          if (envelope.websites?.length) {
            this.websites.set([...envelope.websites].sort((a, b) => a.sortOrder - b.sortOrder));
          }
          if (envelope.values) {
            this.values.set(envelope.values);
          }
          this.loadedFromCentral.set(true);
        }),
        catchError(() => of(null)),
        shareReplay(1)
      );
      this.loadOnce$.subscribe();
    }
  }
}
