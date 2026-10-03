// The legacy JSON contract (Web API 2), which the new API reproduces: the same types serve both.

export interface LegacyUser {
  readonly login: string;
  readonly role: string;
  readonly displayName: string;
}

export interface Product {
  readonly id: number;
  readonly sku: string;
  readonly label: string;
  readonly category: string;
  readonly unitPrice: number;
  readonly vatRate: number;
  readonly available: number;
}

export interface Customer {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly city: string | null;
  readonly tier: string;
}

export interface QuoteLine {
  readonly productId: number;
  readonly sku: string;
  readonly label: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly discountPct: number;
  readonly lineHT: number;
  readonly lineVAT: number;
  readonly volumeDiscount: boolean;
}

export interface Quote {
  readonly customer: {
    readonly id: number;
    readonly name: string;
    readonly tier: string;
    readonly tierDiscountPct: number;
  };
  readonly lines: readonly QuoteLine[];
  readonly totalHT: number;
  readonly shippingHT: number;
  readonly totalVAT: number;
  readonly totalTTC: number;
  readonly missingForFreeShipping: number;
  readonly outOfStock: readonly number[];
}

export type RouteMode = 'Legacy' | 'Shadow' | 'New';

export interface ShadowStats {
  readonly total: number;
  readonly matches: number;
  readonly mismatches: number;
  readonly errors: number;
}

export interface MigrationRoute {
  readonly id: string;
  readonly label: string;
  readonly path: string;
  readonly methods: readonly string[];
  readonly mode: RouteMode;
  readonly shadow: ShadowStats;
}

export interface ShadowComparison {
  readonly routeId: string;
  readonly method: string;
  readonly path: string;
  readonly at: string;
  readonly outcome: 'Match' | 'Mismatch' | 'Error';
  readonly differences: readonly string[];
  readonly legacyMs: number;
  readonly newMs: number;
}

export interface MigrationStatus {
  readonly routes: readonly MigrationRoute[];
  readonly recent: readonly ShadowComparison[];
}
