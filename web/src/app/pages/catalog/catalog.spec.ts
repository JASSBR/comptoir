import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Catalog } from './catalog';

describe('Catalog', () => {
  it('filters by family and by text, and flags low stock', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
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

    expect(element.querySelectorAll('.product')).toHaveLength(2);
    expect(element.querySelector('.stock.low')!.textContent).toContain('4');

    element.querySelectorAll<HTMLButtonElement>('.chips button')[2].click();
    fixture.detectChanges();
    expect(element.querySelectorAll('.product')).toHaveLength(1);

    element.querySelectorAll<HTMLButtonElement>('.chips button')[0].click();
    const search = element.querySelector<HTMLInputElement>('input[type=search]')!;
    search.value = 'far';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(element.querySelector('.product strong')!.textContent).toBe('Farine T55');
    http.verify();
  });
});
