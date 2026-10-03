import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Customers } from './customers';

describe('Customers', () => {
  it('groups customers by discount tier and starts a quote for one of them', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Customers);
    fixture.detectChanges();
    http.expectOne('/api/customers').flush([
      { id: 1, code: 'BLG001', name: 'Boulangerie Martin', city: 'Lyon', tier: 'A' },
      { id: 4, code: 'RES004', name: 'Restaurant Le Bouchon', city: 'Lyon', tier: 'c' },
    ]);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-tier="A"]')!.textContent).toContain('Boulangerie Martin');
    expect(element.querySelector('[data-tier="B"]')!.textContent).toContain('Aucun client');
    // Lower-case tiers exist in the legacy data: they count as their upper-case tier, as in the pricing rules.
    expect(element.querySelector('[data-tier="C"]')!.textContent).toContain(
      'Restaurant Le Bouchon',
    );
    expect(
      element.querySelector<HTMLAnchorElement>('[data-tier="A"] a')!.getAttribute('href'),
    ).toBe('/devis?client=1');
    http.verify();
  });
});
