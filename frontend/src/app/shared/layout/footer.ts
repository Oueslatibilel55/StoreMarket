import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-footer',
  imports: [RouterLink, TranslocoPipe],
  template: `
    <footer class="mt-16 border-t border-line bg-card print:hidden">
      <div class="mx-auto flex max-w-7xl flex-col gap-4 px-4 py-8 text-sm text-muted sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
        <div class="flex items-center gap-2.5">
          <span class="flex size-6 items-center justify-center rounded-md bg-brand text-xs font-bold text-white">
            {{ ('app.name' | transloco).charAt(0) }}
          </span>
          <span class="font-semibold text-ink">{{ 'app.name' | transloco }}</span>
          <span class="hidden sm:inline">·</span>
          <span class="hidden sm:inline">{{ 'app.tagline' | transloco }}</span>
        </div>
        <nav class="flex flex-wrap gap-x-5 gap-y-2">
          <a routerLink="/products" class="transition-colors hover:text-ink">{{ 'shop.products' | transloco }}</a>
          <a routerLink="/stores" class="transition-colors hover:text-ink">{{ 'shop.stores' | transloco }}</a>
          <a routerLink="/orders/track" class="transition-colors hover:text-ink">{{ 'orders.track.title' | transloco }}</a>
        </nav>
      </div>
    </footer>
  `,
})
export class Footer {}
