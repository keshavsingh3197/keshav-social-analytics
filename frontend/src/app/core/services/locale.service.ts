import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export type LanguageCode = 'en' | 'hi';

const STORAGE_KEY = 'social.language';
const OVERRIDDEN_KEY = 'social.language.visitorSet';

export const SUPPORTED_LANGUAGES: { code: LanguageCode; label: string; shortLabel: string }[] = [
  { code: 'en', label: 'English', shortLabel: 'EN' },
  { code: 'hi', label: 'हिन्दी (Hindi)', shortLabel: 'हिं' },
];

export interface LocalizationPolicy {
  siteKey: string;
  defaultLocale: LanguageCode;
  enabledLocales: string[];
  allowVisitorLanguageSwitch: boolean;
  showLanguagePicker: boolean;
  forceLocale: boolean;
  controlledBy: string;
  overrideCentralLocalization: boolean;
}

export interface SiteLocalizationConfigDto {
  id?: string;
  overrideCentralLocalization: boolean;
  defaultLocale: LanguageCode;
  enabledLocales: string[];
  allowVisitorLanguageSwitch: boolean;
  showLanguagePicker: boolean;
  forceLocale: boolean;
}

export interface SiteLocalizationAdminState {
  centralAdminPolicy: LocalizationPolicy;
  localSettings: SiteLocalizationConfigDto;
  effectivePolicy: LocalizationPolicy;
}

interface BundleResponse {
  locale: LanguageCode;
  version: string;
  policy: LocalizationPolicy;
  entries: Record<string, string>;
  source: string;
}

const BUILTIN_EN: Record<string, string> = {
  'social.nav.dashboard': 'Dashboard',
  'social.nav.outliers': 'Outliers & Growth',
  'social.nav.earnings': 'Earnings & RPM',
  'social.nav.advisor': 'Growth Advisor',
  'social.nav.funnel': 'Traffic Funnel',
  'social.nav.admin': 'Admin',
  'social.brand.sub': 'Creator & Social Intelligence',
  'social.hero.title': 'Social Channel Performance & Growth Hub',
  'social.hero.subtitle': 'Search any YouTube or social handle for instant analytics, RPM forecasts, and growth insights — or sign in to connect your own private channels.',
  'social.earnings.title': '💰 Creator Earnings & RPM Calculator',
  'social.advisor.title': '🚀 Growth Advisor & Channel Fix Studio',
  'social.funnel.title': '🎯 Traffic Funnel & Link Conversion Engine',
  'social.kpi.viewsPerSub': 'Views / Subscriber (Outlier Score)',
  'ecosystem.language.label': 'Language',
};

const BUILTIN_HI: Record<string, string> = {
  'social.nav.dashboard': 'डैशबोर्ड',
  'social.nav.outliers': 'आउटलायर व ग्रोथ बेंचमार्क',
  'social.nav.earnings': 'कमाई व RPM',
  'social.nav.advisor': 'ग्रोथ एडवाइज़र',
  'social.nav.funnel': 'ट्रैफिक फ़नल',
  'social.nav.admin': 'एडमिन',
  'social.brand.sub': 'क्रिएटर एनालिटिक्स एवं ग्रोथ हब',
  'social.hero.title': 'सोशल चैनल प्रदर्शन और ग्रोथ हब',
  'social.hero.subtitle': 'YouTube, Instagram और Facebook पर लाइव एंगेजमेंट ट्रैक करें, कमाई का अनुमान लगाएं और दर्शक ट्रैफिक बढ़ाएं।',
  'social.earnings.title': '💰 क्रिएटर कमाई एवं RPM कैलकुलेटर',
  'social.advisor.title': '🚀 ग्रोथ एडवाइज़र एवं चैनल फ़िक्स स्टूडियो',
  'social.funnel.title': '🎯 ट्रैफिक फ़नल और लिंक कन्वर्ज़न इंजन',
  'social.kpi.viewsPerSub': 'व्यूज़ / सब्सक्राइबर (आउटलायर स्कोर)',
  'ecosystem.language.label': 'भाषा',
};

@Injectable({ providedIn: 'root' })
export class LocaleService {
  private http = inject(HttpClient);
  private loaded = false;

  readonly language = signal<LanguageCode>(this.readStored());
  readonly policy = signal<LocalizationPolicy>({
    siteKey: 'social',
    defaultLocale: 'en',
    enabledLocales: ['en', 'hi'],
    allowVisitorLanguageSwitch: true,
    showLanguagePicker: true,
    forceLocale: false,
    controlledBy: 'admin',
    overrideCentralLocalization: false,
  });
  readonly entries = signal<Record<string, string>>({});

  readonly showPicker = computed(() => {
    const p = this.policy();
    return p.showLanguagePicker && !p.forceLocale && p.enabledLocales.length > 1;
  });

  readonly canSwitch = computed(() => {
    const p = this.policy();
    return p.allowVisitorLanguageSwitch && !p.forceLocale;
  });

  readonly availableLanguages = computed(() => {
    const enabled = new Set(this.policy().enabledLocales.map(x => x.toLowerCase()));
    return SUPPORTED_LANGUAGES.filter(l => enabled.has(l.code));
  });

  constructor() {
    effect(() => {
      const lang = this.language();
      localStorage.setItem(STORAGE_KEY, lang);
      if (typeof document !== 'undefined') {
        document.documentElement.lang = lang;
      }
    });
    this.init();
  }

  init(): void {
    if (this.loaded) return;
    this.loaded = true;
    const visitorSet = localStorage.getItem(OVERRIDDEN_KEY) === '1';
    this.http
      .get<BundleResponse>(`${environment.apiUrl}/i18n/bundle/${this.language()}`)
      .pipe(catchError(() => of(null)))
      .subscribe(res => {
        if (!res) return;
        this.policy.set(res.policy);
        this.entries.set(res.entries || {});
        if (res.policy.forceLocale || !res.policy.allowVisitorLanguageSwitch || !visitorSet) {
          const target = this.normalize(res.policy.defaultLocale);
          if (target !== this.language()) {
            this.language.set(target);
            this.fetchBundle(target);
          }
        }
      });
  }

  set(code: LanguageCode, visitorChoice = true): void {
    if (visitorChoice && !this.canSwitch()) return;
    const normalized = this.normalize(code);
    if (visitorChoice) {
      localStorage.setItem(OVERRIDDEN_KEY, '1');
    }
    this.language.set(normalized);
    this.fetchBundle(normalized);
  }

  t(key: string, fallback?: string): string {
    const map = this.entries();
    if (map[key]) return map[key];
    const builtIn = this.language() === 'hi' ? BUILTIN_HI[key] : BUILTIN_EN[key];
    return builtIn || BUILTIN_EN[key] || fallback || key;
  }

  getSiteLocalizationAdminState() {
    return this.http.get<SiteLocalizationAdminState>(`${environment.apiUrl}/settings/localization`);
  }

  updateSiteLocalization(req: SiteLocalizationConfigDto) {
    return this.http
      .put<SiteLocalizationAdminState>(`${environment.apiUrl}/settings/localization`, req)
      .pipe(
        tap(state => {
          this.policy.set(state.effectivePolicy);
          if (state.effectivePolicy.forceLocale || !state.effectivePolicy.allowVisitorLanguageSwitch) {
            this.set(this.normalize(state.effectivePolicy.defaultLocale), false);
          } else {
            this.fetchBundle(this.language());
          }
        })
      );
  }

  private fetchBundle(code: LanguageCode): void {
    this.http
      .get<BundleResponse>(`${environment.apiUrl}/i18n/bundle/${code}`)
      .pipe(catchError(() => of(null)))
      .subscribe(res => {
        if (!res) return;
        this.policy.set(res.policy);
        this.entries.set(res.entries || {});
      });
  }

  private normalize(raw: string | null | undefined): LanguageCode {
    return raw?.toLowerCase().startsWith('hi') ? 'hi' : 'en';
  }

  private readStored(): LanguageCode {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === 'hi' ? 'hi' : 'en';
  }
}
