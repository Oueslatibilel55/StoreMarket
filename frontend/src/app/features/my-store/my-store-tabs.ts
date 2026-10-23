import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { BillingBanner } from '../../shared/billing/billing-banner';

const TABS = [
  { key: 'store', link: '/my-store', exact: true },
  { key: 'stats', link: '/my-store/stats', exact: false },
  { key: 'orders', link: '/my-store/orders', exact: false },
  { key: 'products', link: '/my-store/products', exact: false },
  { key: 'affiliates', link: '/my-store/affiliates', exact: false },
  { key: 'reviews', link: '/my-store/reviews', exact: false },
  { key: 'billing', link: '/my-store/billing', exact: false },
];

/** Tabs at the top of the store owner pages, with the billing alert (025) above them. */
@Component({
  selector: 'app-my-store-tabs',
  imports: [RouterLink, RouterLinkActive, TranslocoPipe, BillingBanner],
  template: `
    <app-billing-banner />
    <nav class="mb-8 grid grid-cols-3 gap-1 rounded-xl border border-line bg-card p-1 shadow-card sm:inline-flex sm:flex-wrap print:hidden"
      [attr.aria-label]="'myStore.nav' | transloco">
      @for (t of tabs; track t.key) {
        <a [routerLink]="t.link" routerLinkActive="!bg-brand-soft !text-brand" [routerLinkActiveOptions]="{ exact: t.exact }"
          class="truncate rounded-lg px-2 py-2 text-center text-sm font-semibold text-muted transition-colors hover:bg-subtle hover:text-ink sm:px-4">
          {{ 'myStore.tabs.' + t.key | transloco }}
        </a>
      }
    </nav>
  `,
})
export class MyStoreTabs {
  protected readonly tabs = TABS;
}
