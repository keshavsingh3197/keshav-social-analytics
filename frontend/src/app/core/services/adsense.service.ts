import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { AdMonetizationConfig } from '../models/analytics.models';

const CLIENT_ID_RE = /^ca-pub-\d{10,20}$/;
const SLOT_ID_RE = /^\d{6,15}$/;

@Injectable({ providedIn: 'root' })
export class AdSenseService {
  private readonly doc = inject(DOCUMENT);
  private loadedClientId: string | null = null;

  isValidClientId(value?: string | null): value is string {
    return !!value && CLIENT_ID_RE.test(value.trim());
  }

  isValidSlotId(value?: string | null): value is string {
    return !!value && SLOT_ID_RE.test(value.trim());
  }

  safeSponsorUrl(raw?: string | null): string | null {
    if (!raw) return null;
    const trimmed = raw.trim();
    try {
      const parsed = new URL(trimmed);
      if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
        return parsed.toString();
      }
      return null;
    } catch {
      return null;
    }
  }

  ensureScriptLoaded(clientId?: string | null): boolean {
    if (!this.isValidClientId(clientId)) return false;
    const clean = clientId.trim();
    if (this.loadedClientId === clean) return true;

    const existing = this.doc.querySelector<HTMLScriptElement>(
      'script[data-socialpulse-adsense="true"], script[src*="pagead/js/adsbygoogle.js"]'
    );
    if (existing) {
      this.loadedClientId = clean;
      return true;
    }

    const script = this.doc.createElement('script');
    script.async = true;
    script.crossOrigin = 'anonymous';
    script.dataset['socialpulseAdsense'] = 'true';
    script.src = `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${encodeURIComponent(clean)}`;
    this.doc.head.appendChild(script);
    this.loadedClientId = clean;
    return true;
  }

  slotForPlacement(ads: AdMonetizationConfig | undefined, placement: 'header' | 'infeed' | 'sidebar' | 'footer'): string | null {
    if (!ads) return null;
    const raw =
      placement === 'header' ? ads.headerAdSlotId :
      placement === 'infeed' ? ads.inFeedAdSlotId :
      placement === 'sidebar' ? ads.sidebarAdSlotId :
      ads.footerAdSlotId;
    return this.isValidSlotId(raw) ? raw.trim() : null;
  }

  pushAdSlot(): void {
    try {
      const win = this.doc.defaultView as (Window & { adsbygoogle?: unknown[] }) | null;
      if (!win) return;
      win.adsbygoogle = win.adsbygoogle || [];
      win.adsbygoogle.push({});
    } catch {
      // Ignore ad-blocker or duplicate slot push errors cleanly
    }
  }
}
