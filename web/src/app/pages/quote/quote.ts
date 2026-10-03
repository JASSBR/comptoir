import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ToastService } from '../../core/toast';
import { Customer, Product, Quote } from '../../models';
import { Icon } from '../../shared/icon';

interface DraftLine {
  readonly key: number;
  readonly productId: number | null;
  readonly quantity: number;
}

const FREE_SHIPPING_FROM = 300;

@Component({
  selector: 'app-quote',
  imports: [CurrencyPipe, DecimalPipe, Icon],
  templateUrl: './quote.html',
  styleUrl: './quote.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuoteBuilder {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);
  private nextKey = 1;

  protected readonly customers = httpResource<Customer[]>(() => '/api/customers', {
    defaultValue: [],
  });
  protected readonly products = httpResource<Product[]>(() => '/api/products', {
    defaultValue: [],
  });

  protected readonly customerId = signal<number | null>(null);
  protected readonly lines = signal<readonly DraftLine[]>([
    { key: 0, productId: null, quantity: 1 },
  ]);
  protected readonly creating = signal(false);
  protected readonly createdId = signal<number | null>(null);

  private readonly request = computed(() => {
    const customerId = this.customerId();
    const lines = this.lines().filter((line) => line.productId !== null && line.quantity > 0);
    return customerId === null || lines.length === 0
      ? undefined
      : {
          customerId,
          lines: lines.map(({ productId, quantity }) => ({ productId: productId!, quantity })),
        };
  });

  // Priced by the new domain on every change: the legacy could only price an order once saved.
  protected readonly quote = httpResource<Quote>(() => {
    const body = this.request();
    return body ? { url: '/api/v2/quotes', method: 'POST', body } : undefined;
  });

  protected readonly freeShippingProgress = computed(() => {
    const quote = this.quote.value();
    return quote ? Math.min(1, quote.totalHT / FREE_SHIPPING_FROM) : 0;
  });

  protected setCustomer(value: string): void {
    this.customerId.set(value ? Number(value) : null);
    this.createdId.set(null);
  }

  protected setProduct(key: number, value: string): void {
    this.update(key, { productId: value ? Number(value) : null });
  }

  protected setQuantity(key: number, value: string): void {
    this.update(key, { quantity: Math.max(0, Math.floor(Number(value) || 0)) });
  }

  protected addLine(): void {
    this.lines.update((lines) => [...lines, { key: this.nextKey++, productId: null, quantity: 1 }]);
  }

  protected removeLine(key: number): void {
    this.lines.update((lines) => lines.filter((line) => line.key !== key));
  }

  protected isShort(productId: number | null): boolean {
    return productId !== null && (this.quote.value()?.outOfStock.includes(productId) ?? false);
  }

  /** The draft is created through the same contract the 2014 screens use; it opens in the legacy order screen. */
  protected async createDraft(): Promise<void> {
    const body = this.request();
    if (!body) return;
    this.creating.set(true);
    try {
      const created = await firstValueFrom(this.http.post<{ id: number }>('/api/orders', body));
      this.createdId.set(created.id);
      this.toasts.show({ tone: 'success', title: `Brouillon n° ${created.id} créé` });
    } catch {
      this.toasts.show({ tone: 'error', title: 'Le brouillon n’a pas pu être créé.' });
    } finally {
      this.creating.set(false);
    }
  }

  private update(key: number, change: Partial<DraftLine>): void {
    this.lines.update((lines) =>
      lines.map((line) => (line.key === key ? { ...line, ...change } : line)),
    );
    this.createdId.set(null);
  }
}
