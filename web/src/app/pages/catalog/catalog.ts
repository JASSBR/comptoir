import { CurrencyPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { Product } from '../../models';

const LOW_STOCK = 20;

@Component({
  selector: 'app-catalog',
  imports: [CurrencyPipe],
  templateUrl: './catalog.html',
  styleUrl: './catalog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Catalog {
  protected readonly products = httpResource<Product[]>(() => '/api/products', {
    defaultValue: [],
  });
  protected readonly search = signal('');
  protected readonly category = signal('');
  protected readonly lowStock = LOW_STOCK;

  protected readonly categories = computed(() => [
    ...new Set(this.products.value().map((p) => p.category)),
  ]);
  protected readonly visible = computed(() => {
    const search = this.search().trim().toLowerCase();
    const category = this.category();
    return this.products
      .value()
      .filter(
        (p) =>
          (!category || p.category === category) &&
          (!search ||
            p.label.toLowerCase().includes(search) ||
            p.sku.toLowerCase().includes(search)),
      );
  });
}
