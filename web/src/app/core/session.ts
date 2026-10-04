import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LegacyUser } from '../models';

/**
 * The 2014 application is still the identity provider: its Forms cookie is shared with this app because the facade
 * serves both on one origin. The sign-in screen is this app's (/app/connexion): the facade posts the credentials to
 * the legacy form and hands its cookie back (ADR 0010). GET /api/session (still answered by the legacy) tells who is
 * signed in; without a session the legacy redirects to its login page, which is how "signed out" looks from here.
 */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly http = inject(HttpClient);
  readonly user = signal<LegacyUser | null>(null);
  readonly checked = signal(false);
  readonly signedIn = computed(() => this.user() !== null);

  async signIn(login: string, password: string): Promise<void> {
    this.user.set(
      await firstValueFrom(this.http.post<LegacyUser>('/session', { login, password })),
    );
  }

  async signOut(): Promise<void> {
    await firstValueFrom(this.http.delete('/session'));
    this.user.set(null);
  }

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

/** The sign-in screen, coming back to the given path (an /app/… page or a 2014 screen) afterwards. */
export function loginUrl(returnPath: string = globalThis.location?.pathname ?? '/app/'): string {
  return `/app/connexion?returnUrl=${encodeURIComponent(returnPath)}`;
}
