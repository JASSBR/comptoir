import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Session, legacyLoginUrl } from '../core/session';
import { ThemeService } from '../core/theme';
import { Avatar } from '../shared/avatar';
import { Icon } from '../shared/icon';

const REPOSITORY = 'https://github.com/JASSBR/comptoir';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Avatar, Icon],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  protected readonly session = inject(Session);
  protected readonly theme = inject(ThemeService);
  protected readonly repositoryUrl = REPOSITORY;
  protected readonly loginUrl = legacyLoginUrl('/app/devis');
}
