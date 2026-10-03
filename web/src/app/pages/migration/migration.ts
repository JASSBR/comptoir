import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject } from '@angular/core';
import { MigrationStatus, RouteMode } from '../../models';
import { Icon } from '../../shared/icon';

const REFRESH_MS = 5_000;

export const MODE_LABELS: Readonly<Record<RouteMode, string>> = {
  Legacy: 'Application 2014',
  Shadow: 'En vérification',
  New: 'Migrée',
};

@Component({
  selector: 'app-migration',
  imports: [DatePipe, DecimalPipe, PercentPipe, Icon],
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
  protected readonly counts = computed(() => {
    const routes = this.routes();
    return {
      total: routes.length,
      migrated: routes.filter((route) => route.mode === 'New').length,
      shadow: routes.filter((route) => route.mode === 'Shadow').length,
      legacy: routes.filter((route) => route.mode === 'Legacy').length,
    };
  });
  protected readonly progress = computed(() => {
    const { total, migrated } = this.counts();
    return total === 0 ? 0 : migrated / total;
  });

  constructor() {
    // Polling is enough for a dashboard; reload() keeps the current figures on screen while refreshing.
    const timer = setInterval(() => this.status.reload(), REFRESH_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected agreement(matches: number, total: number): number | null {
    return total === 0 ? null : matches / total;
  }
}
