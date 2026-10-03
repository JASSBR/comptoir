import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LegacyUser } from '../models';

/**
 * Users sign in on the 2014 application: its Forms cookie is shared with this app because the facade serves both on
 * one origin. GET /api/session (still answered by the legacy) tells who is signed in; without a session the legacy
 * redirects to its HTML login page, which is how "signed out" looks from here.
 */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly http = inject(HttpClient);
  readonly user = signal<LegacyUser | null>(null);
  readonly checked = signal(false);
  readonly signedIn = computed(() => this.user() !== null);

  async refresh(): Promise<void> {
    try {
      const body = await firstValueFrom(this.http.get('/api/session', { responseType: 'text' }));
      const parsed = JSON.parse(body) as Partial<LegacyUser>;
      this.user.set(
        typeof parsed.login === 'string' && parsed.login ? (parsed as LegacyUser) : null,
      );
    } catch {
      this.user.set(null);
    } finally {
      this.checked.set(true);
    }
  }
}

/** The legacy login page, coming back here afterwards. Url.IsLocalUrl on the legacy side accepts /app/… paths. */
export function legacyLoginUrl(
  returnPath: string = globalThis.location?.pathname ?? '/app/',
): string {
  return `/Account/Login?ReturnUrl=${encodeURIComponent(returnPath)}`;
}
