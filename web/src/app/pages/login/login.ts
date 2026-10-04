import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Session } from '../../core/session';
import { Icon } from '../../shared/icon';

/** The demo accounts are published on purpose: the point is to try the migration, not to guard fictitious data. */
export const DEMO_ACCOUNTS = [
  { login: 'sophie', role: 'Commerciale' },
  { login: 'marc', role: 'Magasin' },
  { login: 'claire', role: 'Administration' },
] as const;
export const DEMO_PASSWORD = 'comptoir-demo';

/**
 * The sign-in screen, migrated (ADR 0010). The 2014 application still checks the password: the facade posts these
 * credentials to its login form and hands its session cookie to the browser, shared by both applications.
 */
@Component({
  selector: 'app-login',
  imports: [Icon],
  templateUrl: './login.html',
  styleUrl: './login.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  /** Where to go once signed in: an /app/… page, or a 2014 screen (bound from the query string). */
  readonly returnUrl = input<string>();

  protected readonly accounts = DEMO_ACCOUNTS;
  protected readonly password = DEMO_PASSWORD;
  protected readonly login = signal('');
  protected readonly secret = signal('');
  protected readonly pending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected pick(login: string): void {
    this.login.set(login);
    this.secret.set(DEMO_PASSWORD);
    this.error.set(null);
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.pending()) return;
    this.pending.set(true);
    this.error.set(null);
    try {
      await this.session.signIn(this.login().trim(), this.secret());
      await this.goBack();
    } catch (error) {
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 401
          ? ((error.error as { message?: string } | null)?.message ?? 'Connexion refusée.')
          : 'Le serveur de démonstration se réveille. Réessayez dans quelques secondes.',
      );
      this.pending.set(false);
    }
  }

  private async goBack(): Promise<void> {
    const target = this.returnUrl() ?? '';
    // Only local paths: an absolute or protocol-relative URL here would be an open redirect.
    const local = target.startsWith('/') && !target.startsWith('//') ? target : '/app/migration';
    if (local.startsWith('/app/') || local === '/app') {
      await this.router.navigateByUrl(local.slice('/app'.length) || '/');
    } else {
      globalThis.location.assign(local);
    }
  }
}
