import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-footer',
  imports: [RouterLink, TranslocoPipe],
  template: `
    <footer class="mt-16 bg-ink text-white/70 print:hidden">
      <div class="mx-auto flex max-w-7xl flex-col gap-6 px-4 py-10 text-sm sm:flex-row sm:items-end sm:justify-between sm:px-6 lg:px-8">
        <div class="max-w-sm">
          <p class="flex items-center gap-2.5">
            <span class="arch flex h-8 w-6 items-end justify-center bg-brand pb-1 font-display text-xs font-extrabold text-white" aria-hidden="true">
              {{ ('app.name' | transloco).charAt(0) }}
            </span>
            <span class="font-display text-lg font-extrabold tracking-tight text-white">{{ 'app.name' | transloco }}</span>
          </p>
          <p class="mt-3 leading-relaxed">{{ 'app.tagline' | transloco }}</p>
        </div>
        <nav class="flex flex-wrap gap-x-6 gap-y-2">
          <a routerLink="/products" class="transition-colors hover:text-white">{{ 'shop.products' | transloco }}</a>
          <a routerLink="/stores" class="transition-colors hover:text-white">{{ 'shop.stores' | transloco }}</a>
          <a routerLink="/orders/track" class="transition-colors hover:text-white">{{ 'orders.track.title' | transloco }}</a>
        </nav>
      </div>
    </footer>
  `,
})
export class Footer {}
