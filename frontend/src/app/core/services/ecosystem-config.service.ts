import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppConfigEnvelope, CentralChannel, EcosystemWebsite } from '../models/analytics.models';

@Injectable({ providedIn: 'root' })
export class EcosystemConfigService {
  private http = inject(HttpClient);
  private loadOnce$?: ReturnType<HttpClient['get']>;

  readonly websites = signal<EcosystemWebsite[]>([]);
  readonly channels = signal<CentralChannel[]>([]);
  readonly values = signal<Record<string, string>>({});
  readonly loadedFromCentral = signal(false);

  readonly ecosystemBarSites = computed(() =>
    this.websites()
      .filter(w => w.showInEcosystemBar !== false && !['blog', 'blog-admin', 'admin', 'ghar-ledger'].includes((w.key || '').toLowerCase()))
      .sort((a, b) => a.sortOrder - b.sortOrder)
  );

  readonly promoSiblingSites = computed(() =>
    this.websites()
      .filter(w => !['social', 'admin', 'blog', 'blog-admin', 'ghar-ledger'].includes((w.key || '').toLowerCase()))
      .sort((a, b) => a.sortOrder - b.sortOrder)
  );

  readonly footerSites = computed(() =>
    this.websites()
      .filter(w => w.showInEcosystemBar !== false && !['blog', 'blog-admin', 'admin', 'ghar-ledger'].includes((w.key || '').toLowerCase()))
      .sort((a, b) => a.sortOrder - b.sortOrder)
  );

  readonly footerChannels = computed(() =>
    this.channels().filter(c => c.isEnabled !== false && c.showInFooter !== false)
  );

  readonly primaryYouTubeChannel = computed(() =>
    this.channels().find(c => c.isEnabled !== false && c.platform?.toLowerCase() === 'youtube' && c.isPrimary)
    ?? this.channels().find(c => c.isEnabled !== false && c.platform?.toLowerCase() === 'youtube')
    ?? null
  );

  readonly creatorName = computed(() =>
    this.values()['ecosystem.creatorName'] || ''
  );

  readonly copyrightNotice = computed(() =>
    this.values()['ecosystem.copyrightNotice'] || ''
  );

  readonly youtubeChannelName = computed(() =>
    this.primaryYouTubeChannel()?.name || this.values()['ecosystem.youtubeChannelName'] || ''
  );

  readonly youtubeChannelUrl = computed(() =>
    this.primaryYouTubeChannel()?.url || this.values()['ecosystem.youtubeChannelUrl'] || ''
  );

  readonly featuredPromoText = computed(() =>
    this.values()['social.featuredPromoText'] || ''
  );

  readonly adminPortalUrl = computed(() =>
    this.websites().find(w => w.key === 'admin')?.url ||
    this.values()['ecosystem.adminUrl'] ||
    'https://admin.keshavsingh.in'
  );

  readonly studioPortalUrl = computed(() =>
    this.websites().find(w => w.key === 'studio')?.url || ''
  );

  load(): void {
    if (!this.loadOnce$) {
      const configUrl = `${environment.idpUrl}/config`;
      this.loadOnce$ = this.http.get<AppConfigEnvelope>(configUrl).pipe(
        tap(envelope => {
          if (envelope.websites?.length) {
            this.websites.set([...envelope.websites].sort((a, b) => a.sortOrder - b.sortOrder));
          }
          if (envelope.channels?.length) {
            this.channels.set([...envelope.channels]);
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
