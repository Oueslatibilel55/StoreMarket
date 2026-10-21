import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

/** Tabs at the top of the store owner pages. */
@Component({
  selector: 'app-my-store-tabs',
  imports: [RouterLink, RouterLinkActive, TranslocoPipe],
  template: `
    <nav class="mb-8 grid grid-cols-2 gap-1 sm:grid-cols-4 rounded-xl border border-line bg-card p-1 shadow-card sm:inline-flex" [attr.aria-label]="'myStore.nav' | transloco">
      <a routerLink="/my-store" routerLinkActive="!bg-brand-soft !text-brand" [routerLinkActiveOptions]="{ exact: true }"
        class="truncate rounded-lg px-2 py-2 text-center text-sm font-semibold sm:px-4 text-muted transition-colors hover:bg-subtle hover:text-ink">{{ 'myStore.tabs.store' | transloco }}</a>
      <a routerLink="/my-store/orders" routerLinkActive="!bg-brand-soft !text-brand"
        class="truncate rounded-lg px-2 py-2 text-center text-sm font-semibold text-muted transition-colors hover:bg-subtle hover:text-ink sm:px-4">{{ 'myStore.tabs.orders' | transloco }}</a>
      <a routerLink="/my-store/products" routerLinkActive="!bg-brand-soft !text-brand"
        class="truncate rounded-lg px-2 py-2 text-center text-sm font-semibold sm:px-4 text-muted transition-colors hover:bg-subtle hover:text-ink">{{ 'myStore.tabs.products' | transloco }}</a>
      <a routerLink="/my-store/affiliates" routerLinkActive="!bg-brand-soft !text-brand"
        class="truncate rounded-lg px-2 py-2 text-center text-sm font-semibold sm:px-4 text-muted transition-colors hover:bg-subtle hover:text-ink">{{ 'myStore.tabs.affiliates' | transloco }}</a>
    </nav>
  `,
})
export class MyStoreTabs {}
