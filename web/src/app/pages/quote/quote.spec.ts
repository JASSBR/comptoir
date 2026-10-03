import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { registerLocaleData } from '@angular/common';
import localeFr from '@angular/common/locales/fr';
import { DEFAULT_CURRENCY_CODE, LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ToastService } from '../../core/toast';
import { quote, settle } from '../../testing';
import { QuoteBuilder } from './quote';

describe('QuoteBuilder', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    registerLocaleData(localeFr);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: LOCALE_ID, useValue: 'fr-FR' },
        { provide: DEFAULT_CURRENCY_CODE, useValue: 'EUR' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render() {
    const fixture = TestBed.createComponent(QuoteBuilder);
    fixture.detectChanges();
    http
      .expectOne('/api/customers')
      .flush([{ id: 5, code: 'BLG005', name: 'La Mie Doree', city: 'Bron', tier: 'B' }]);
    http.expectOne('/api/products').flush([
      {
        id: 2,
        sku: 'FAR-T65-25',
        label: 'Farine T65',
        category: 'Farines',
        unitPrice: 19.4,
        vatRate: 5.5,
        available: 238,
      },
    ]);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const choose = async (selector: string, value: string, event = 'change') => {
      const field = element.querySelector<HTMLInputElement | HTMLSelectElement>(selector)!;
      field.value = value;
      field.dispatchEvent(new Event(event));
      await settle();
      fixture.detectChanges();
    };
    return { fixture, element, choose };
  }

  it('asks for nothing until a customer and an item are chosen', async () => {
    const { element } = await render();
    http.expectNone('/api/v2/quotes');
    expect(element.querySelector('.blank')).not.toBeNull();
  });

  it('prices the order through the new domain as it is typed', async () => {
    const { element, choose, fixture } = await render();
    await choose('[data-field=customer]', '5');
    await choose('[data-field=product]', '2');
    await choose('[data-field=quantity]', '120', 'input');

    const request = http.match('/api/v2/quotes').at(-1)!;
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      customerId: 5,
      lines: [{ productId: 2, quantity: 120 }],
    });
    request.flush(quote());
    await fixture.whenStable();

    expect(element.querySelector('.tag')!.textContent).toContain('remise volume');
    expect(element.querySelector('[data-field=total]')!.textContent).toContain('2');
    expect(element.querySelector('.stamp.franco')).not.toBeNull();
  });

  it('shows how much is missing for free shipping, and blocks a quote that cannot be served', async () => {
    const { element, choose, fixture } = await render();
    await choose('[data-field=customer]', '5');
    await choose('[data-field=product]', '2');
    http
      .match('/api/v2/quotes')
      .at(-1)!
      .flush(quote({ shippingHT: 15, missingForFreeShipping: 42.5, outOfStock: [2] }));
    await fixture.whenStable();

    expect(element.querySelector('.shipping')!.textContent).toContain('42,50');
    expect(element.querySelector('.tag.short')).not.toBeNull();
    expect(element.querySelector<HTMLButtonElement>('button.primary')!.disabled).toBe(true);
  });

  it('creates the draft through the legacy contract and links to the legacy order screen', async () => {
    const { element, choose, fixture } = await render();
    await choose('[data-field=customer]', '5');
    await choose('[data-field=product]', '2');
    http.match('/api/v2/quotes').at(-1)!.flush(quote());
    await fixture.whenStable();

    element.querySelector<HTMLButtonElement>('button.primary')!.click();
    const create = http.expectOne((r) => r.url === '/api/orders' && r.method === 'POST');
    expect(create.request.body).toEqual({ customerId: 5, lines: [{ productId: 2, quantity: 1 }] });
    create.flush({ id: 41 });
    await settle();
    fixture.detectChanges();

    expect(element.querySelector<HTMLAnchorElement>('a.primary')!.getAttribute('href')).toBe(
      '/#!/commandes/41',
    );
    expect(TestBed.inject(ToastService).toasts()[0].tone).toBe('success');
  });

  it('adds and removes lines', async () => {
    const { element, fixture } = await render();
    element.querySelector<HTMLButtonElement>('.add')!.click();
    fixture.detectChanges();
    expect(element.querySelectorAll('.line:not(.head)')).toHaveLength(2);

    element.querySelector<HTMLButtonElement>('.line:not(.head) button')!.click();
    fixture.detectChanges();
    expect(element.querySelectorAll('.line:not(.head)')).toHaveLength(1);
  });

  it('reports a draft that could not be created', async () => {
    const { element, choose, fixture } = await render();
    await choose('[data-field=customer]', '5');
    await choose('[data-field=product]', '2');
    http.match('/api/v2/quotes').at(-1)!.flush(quote());
    await fixture.whenStable();

    element.querySelector<HTMLButtonElement>('button.primary')!.click();
    http
      .expectOne((r) => r.url === '/api/orders')
      .flush({ message: 'Client inconnu.' }, { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(TestBed.inject(ToastService).toasts()[0].tone).toBe('error');
  });
});
