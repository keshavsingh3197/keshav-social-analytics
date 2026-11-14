import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationStart,
  Router,
} from '@angular/router';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly router = inject(Router, { optional: true });
  private readonly destroyRef = inject(DestroyRef);

  private readonly _httpCount = signal(0);
  private readonly _navigating = signal(false);
  private readonly _slow = signal(false);
  private slowTimer: ReturnType<typeof setTimeout> | null = null;
  private activeSince = 0;

  readonly httpCount = this._httpCount.asReadonly();
  readonly navigating = this._navigating.asReadonly();
  readonly active = computed(() => this._httpCount() > 0 || this._navigating());
  readonly slow = this._slow.asReadonly();

  constructor() {
    if (this.router) {
      this.router.events
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((ev) => {
          if (ev instanceof NavigationStart) {
            this._navigating.set(true);
            this.syncSlowTimer();
          } else if (
            ev instanceof NavigationEnd ||
            ev instanceof NavigationCancel ||
            ev instanceof NavigationError
          ) {
            this._navigating.set(false);
            this.syncSlowTimer();
          }
        });
    }

    this.destroyRef.onDestroy(() => this.clearSlowTimer());
  }

  beginRequest(): void {
    this._httpCount.update((c) => c + 1);
    this.syncSlowTimer();
  }

  endRequest(): void {
    this._httpCount.update((c) => Math.max(0, c - 1));
    this.syncSlowTimer();
  }

  private syncSlowTimer(): void {
    if (this.active()) {
      if (!this.activeSince) {
        this.activeSince = Date.now();
      }
      if (!this.slowTimer && !this._slow()) {
        const delayMs = Math.max(0, 2200 - (Date.now() - this.activeSince));
        this.slowTimer = setTimeout(() => {
          this.slowTimer = null;
          if (this.active()) {
            this._slow.set(true);
          }
        }, delayMs);
      }
    } else {
      this.activeSince = 0;
      this.clearSlowTimer();
      if (this._slow()) {
        this._slow.set(false);
      }
    }
  }

  private clearSlowTimer(): void {
    if (this.slowTimer) {
      clearTimeout(this.slowTimer);
      this.slowTimer = null;
    }
  }
}
