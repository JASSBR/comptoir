import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';

/** What load-state needs from an httpResource. */
export interface Loadable {
  isLoading(): boolean;
  error(): unknown;
  hasValue(): boolean;
  reload(): boolean;
}

const RETRY_EVERY_MS = 6_000;
// Two minutes in all: a serverless Azure SQL database resumes in up to a minute or two after auto-pause.
const MAX_AUTOMATIC_RETRIES = 20;

/**
 * Loading and failure, said plainly. The demo runs on free tiers that sleep: the first call after a quiet period can
 * fail while the database and the containers wake up, so a failure is retried by itself for about two minutes.
 */
@Component({
  selector: 'app-load-state',
  template: `
    @if (resource().error()) {
      <div class="note error" role="alert">
        <strong>Les données ne sont pas encore arrivées.</strong>
        <p>
          Les serveurs de démonstration se mettent en veille quand personne ne les utilise ; ils
          redémarrent en une à deux minutes.
          @if (retries() < max) {
            Nouvel essai automatique dans quelques secondes.
          }
        </p>
        <button type="button" class="button small" (click)="retryNow()">
          Réessayer maintenant
        </button>
      </div>
    } @else if (resource().isLoading() && !resource().hasValue()) {
      <div class="lines" aria-busy="true" aria-label="Chargement">
        @for (row of placeholder(); track $index) {
          <span class="skeleton"></span>
        }
      </div>
    }
  `,
  styles: `
    .note {
      display: grid;
      gap: 0.5rem;
      justify-items: start;
    }
    .lines {
      display: grid;
      gap: 0.7rem;
      padding-block: 0.5rem;
    }
    .skeleton {
      height: 1.1rem;
    }
    .skeleton:nth-child(3n) {
      width: 72%;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadState {
  readonly resource = input.required<Loadable>();
  readonly rows = input(5);

  protected readonly max = MAX_AUTOMATIC_RETRIES;
  protected readonly retries = signal(0);
  protected readonly placeholder = () => Array.from({ length: this.rows() });
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    effect(() => {
      const failed = this.resource().error() !== undefined;
      untracked(() => (failed ? this.schedule() : this.cancel()));
    });
    inject(DestroyRef).onDestroy(() => this.cancel());
  }

  protected retryNow(): void {
    this.cancel();
    this.resource().reload();
  }

  private schedule(): void {
    if (this.timer || this.retries() >= MAX_AUTOMATIC_RETRIES) return;
    this.timer = setTimeout(() => {
      this.timer = undefined;
      this.retries.update((count) => count + 1);
      this.resource().reload();
    }, RETRY_EVERY_MS);
  }

  private cancel(): void {
    clearTimeout(this.timer);
    this.timer = undefined;
  }
}
