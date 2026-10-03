import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { migrationStatus } from '../../testing';
import { Migration } from './migration';

describe('Migration', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  async function render() {
    const fixture = TestBed.createComponent(Migration);
    fixture.detectChanges();
    http.expectOne('/migration/status').flush(migrationStatus());
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('shows the plan without the catch-all route, sorted onto the three copies', async () => {
    const { element } = await render();

    expect(element.querySelectorAll('tbody tr')).toHaveLength(3);
    expect(element.querySelector('[data-route="legacy"]')).toBeNull();
    expect(element.querySelector('.copy[data-mode="New"] .count')!.textContent).toBe('1');
    expect(element.querySelector('.copy[data-mode="Legacy"] li')!.textContent).toBe('Facturation');
    expect(element.querySelector('[data-route="orders-list"] .stamp')!.textContent).toBe(
      'En contrôle',
    );
  });

  it('shows the agreement of shadow traffic and the latest differences', async () => {
    const { element } = await render();

    expect(element.querySelector('[data-route="orders-list"] .differs')!.textContent).toContain(
      '3 sur 4 identiques',
    );
    expect(element.querySelector('.difference')!.textContent).toBe(
      '$[0].available: legacy 238, new 237',
    );
    expect(element.querySelector('[data-outcome="Mismatch"] .outcome')!.textContent).toBe(
      'Réponses différentes',
    );
  });

  it('refreshes every five seconds and stops when left', async () => {
    const { fixture } = await render();

    vi.advanceTimersByTime(5_000);
    TestBed.tick();
    http.expectOne('/migration/status').flush(migrationStatus({ recent: [] }));
    fixture.destroy();
    vi.advanceTimersByTime(10_000);
    http.expectNone('/migration/status');
  });
});
