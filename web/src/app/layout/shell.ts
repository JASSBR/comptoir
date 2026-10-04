import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Session } from '../core/session';
import { ThemeService } from '../core/theme';
import { Icon } from '../shared/icon';

const REPOSITORY = 'https://github.com/JASSBR/comptoir';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  protected readonly session = inject(Session);
  protected readonly theme = inject(ThemeService);
  protected readonly repositoryUrl = REPOSITORY;
  private readonly router = inject(Router);

  /** The legacy stores roles as codes; people read them as jobs. */
  protected roleLabel(role: string): string {
    return (
      (
        { commercial: 'Commerciale', magasin: 'Magasin', admin: 'Administration' } as Record<
          string,
          string
        >
      )[role] ?? role
    );
  }

  protected async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigate(['/connexion']);
  }
}
