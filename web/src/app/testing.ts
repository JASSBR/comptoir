import { TestBed } from '@angular/core/testing';
import { MigrationStatus, Quote } from './models';

export function migrationStatus(overrides: Partial<MigrationStatus> = {}): MigrationStatus {
  const none = { total: 0, matches: 0, mismatches: 0, errors: 0 };
  return {
    routes: [
      {
        id: 'catalog-products',
        label: 'Catalogue',
        path: '/api/products',
        methods: ['GET'],
        mode: 'New',
        shadow: none,
      },
      {
        id: 'orders-list',
        label: 'Commandes : liste',
        path: '/api/orders',
        methods: ['GET'],
        mode: 'Shadow',
        shadow: { total: 4, matches: 3, mismatches: 1, errors: 0 },
      },
      {
        id: 'orders-invoice',
        label: 'Facturation',
        path: '/api/orders/{id:int}/invoice',
        methods: ['POST'],
        mode: 'Legacy',
        shadow: none,
      },
      {
        id: 'legacy',
        label: 'Écrans AngularJS',
        path: '/{**catch-all}',
        methods: ['*'],
        mode: 'Legacy',
        shadow: none,
      },
    ],
    recent: [
      {
        routeId: 'orders-list',
        method: 'GET',
        path: '/api/orders',
        at: '2026-10-03T10:00:00Z',
        outcome: 'Mismatch',
        differences: ['$[0].available: legacy 238, new 237'],
        legacyMs: 120,
        newMs: 18,
      },
    ],
    ...overrides,
  };
}

export function quote(overrides: Partial<Quote> = {}): Quote {
  return {
    customer: { id: 5, name: 'La Mie Doree', tier: 'B', tierDiscountPct: 5 },
    lines: [
      {
        productId: 2,
        sku: 'FAR-T65-25',
        label: 'Farine T65',
        quantity: 120,
        unitPrice: 19.4,
        discountPct: 8,
        lineHT: 2141.76,
        lineVAT: 117.8,
        volumeDiscount: true,
      },
    ],
    totalHT: 2141.76,
    shippingHT: 0,
    totalVAT: 117.8,
    totalTTC: 2259.56,
    missingForFreeShipping: 0,
    outOfStock: [],
    ...overrides,
  };
}

/** Lets promise callbacks run, then flushes signal effects. */
export async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  TestBed.tick();
}
