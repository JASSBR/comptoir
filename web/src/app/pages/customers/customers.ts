import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Customer } from '../../models';
import { LoadState } from '../../shared/load-state';

/** Discount tiers as the sales team explains them to a customer. */
export const TIERS: readonly {
  readonly code: string;
  readonly discount: number;
  readonly meaning: string;
}[] = [
  { code: 'A', discount: 10, meaning: 'Clients historiques, gros volumes' },
  { code: 'B', discount: 5, meaning: 'Clients réguliers' },
  { code: 'C', discount: 0, meaning: 'Prix catalogue' },
];

@Component({
  selector: 'app-customers',
  imports: [RouterLink, LoadState],
  templateUrl: './customers.html',
  styleUrl: './customers.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Customers {
  protected readonly customers = httpResource<Customer[]>(() => '/api/customers', {
    defaultValue: [],
  });
  protected readonly tiers = computed(() =>
    TIERS.map((tier) => ({
      ...tier,
      customers: this.customers.value().filter((c) => c.tier.toUpperCase() === tier.code),
    })),
  );
}
