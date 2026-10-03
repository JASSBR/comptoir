import { DatePipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject } from '@angular/core';
import { MigrationRoute, MigrationStatus, RouteMode } from '../../models';
import { LoadState } from '../../shared/load-state';

const REFRESH_MS = 5_000;

export const MODE_LABELS: Readonly<Record<RouteMode, string>> = {
  Legacy: '2014',
  Shadow: 'En contrôle',
  New: 'Migré',
};

interface Copy {
  readonly mode: RouteMode;
  readonly title: string;
  readonly caption: string;
  readonly routes: readonly MigrationRoute[];
}

@Component({
  selector: 'app-migration',
  imports: [DatePipe, DecimalPipe, LoadState],
  templateUrl: './migration.html',
  styleUrl: './migration.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Migration {
  protected readonly status = httpResource<MigrationStatus>(() => '/migration/status');
  protected readonly modeLabels = MODE_LABELS;

  protected readonly routes = computed(
    () => this.status.value()?.routes.filter((route) => route.id !== 'legacy') ?? [],
  );

  // The three copies of the order pad: what each application holds today.
  protected readonly copies = computed<Copy[]>(() => {
    const routes = this.routes();
    const of = (mode: RouteMode) => routes.filter((route) => route.mode === mode);
    return [
      {
        mode: 'New',
        title: 'Nouvelle application',
        caption: '.NET 10 répond seul',
        routes: of('New'),
      },
      {
        mode: 'Shadow',
        title: 'En contrôle',
        caption: '2014 répond, .NET 10 est comparé',
        routes: of('Shadow'),
      },
      {
        mode: 'Legacy',
        title: 'Encore en 2014',
        caption: 'pas encore migré',
        routes: of('Legacy'),
      },
    ];
  });

  constructor() {
    // Polling is enough for a dashboard; reload() keeps the current figures on screen while refreshing.
    const timer = setInterval(() => this.status.reload(), REFRESH_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected agreement(route: MigrationRoute): string {
    const { total, matches } = route.shadow;
    return total === 0 ? '' : `${matches} sur ${total} identiques`;
  }
}
