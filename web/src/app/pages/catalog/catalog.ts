import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { Product } from '../../models';
import { LoadState } from '../../shared/load-state';

const LOW_STOCK = 20;

@Component({
  selector: 'app-catalog',
  imports: [CurrencyPipe, DecimalPipe, LoadState],
  templateUrl: './catalog.html',
  styleUrl: './catalog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Catalog {
  protected readonly products = httpResource<Product[]>(() => '/api/products', {
    defaultValue: [],
  });
  protected readonly search = signal('');
  protected readonly lowStock = LOW_STOCK;

  /** The price list, by family, as the sales team prints it. */
  protected readonly families = computed(() => {
    const search = this.search().trim().toLowerCase();
    const matching = this.products
      .value()
      .filter(
        (p) =>
          !search || p.label.toLowerCase().includes(search) || p.sku.toLowerCase().includes(search),
      );
    const families = new Map<string, Product[]>();
    for (const product of matching) {
      families.set(product.category, [...(families.get(product.category) ?? []), product]);
    }
    return [...families].map(([name, products]) => ({ name, products }));
  });
  protected readonly lowCount = computed(
    () => this.products.value().filter((p) => p.available < LOW_STOCK).length,
  );
}
