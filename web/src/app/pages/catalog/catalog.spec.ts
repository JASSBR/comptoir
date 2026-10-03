import { registerLocaleData } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import localeFr from '@angular/common/locales/fr';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Catalog } from './catalog';

describe('Catalog', () => {
  it('lists products by family, finds them by name or reference, and flags low stock', async () => {
    registerLocaleData(localeFr);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: LOCALE_ID, useValue: 'fr-FR' },
      ],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Catalog);
    fixture.detectChanges();
    http.expectOne('/api/products').flush([
      {
        id: 1,
        sku: 'FAR-T55-25',
        label: 'Farine T55',
        category: 'Farines',
        unitPrice: 17.9,
        vatRate: 5.5,
        available: 400,
      },
      {
        id: 2,
        sku: 'MAT-THE-1',
        label: 'Thermomètre sonde',
        category: 'Materiel',
        unitPrice: 34.9,
        vatRate: 20,
        available: 4,
      },
    ]);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect([...element.querySelectorAll('h2')].map((h) => h.textContent)).toEqual([
      'Farines',
      'Materiel',
    ]);
    expect(element.querySelector('.stock.low')!.textContent).toContain('4 en stock');
    expect(element.querySelector('.lede')!.textContent).toContain('1 sous le seuil');
    expect(element.textContent).toContain('TVA 5,5 %');

    const search = element.querySelector<HTMLInputElement>('input[type=search]')!;
    search.value = 'mat-the';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(element.querySelectorAll('.product')).toHaveLength(1);

    search.value = 'introuvable';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(element.querySelector('.list')!.textContent).toContain('Aucun article');
    http.verify();
  });
});
