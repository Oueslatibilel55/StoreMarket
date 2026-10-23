import { Component, effect, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { map } from 'rxjs';
import { CatalogApi } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { Seo } from '../../core/seo/seo';
import { ProductCard } from '../../shared/catalog/product-card';
import { StoreCard } from '../../shared/catalog/store-card';
import { LocalizedPipe } from '../../shared/i18n/localized';

/** Home: store categories, best-rated products (017), newest products, stores (best rated first). */
@Component({
  selector: 'app-home',
  imports: [RouterLink, TranslocoPipe, LocalizedPipe, ProductCard, StoreCard],
  templateUrl: './home.html',
})
export class Home {
  private readonly catalog = inject(CatalogApi);
  protected readonly language = inject(LanguageService);

  /** What the platform offers each audience (`home.roles.<key>.*`). */
  protected readonly roles = [
    { key: 'buyer', icon: '🛍️', tone: 'bg-brand-soft' },
    { key: 'owner', icon: '🏪', tone: 'bg-success/10' },
    { key: 'affiliate', icon: '🪙', tone: 'bg-accent/15' },
  ];

  protected readonly categories = rxResource({ stream: () => this.catalog.storeCategories() });
  /** Rated products only: unrated ones come last in the TopRated sort and are dropped here. */
  protected readonly topRated = rxResource({
    stream: () => this.catalog.products({ sort: 'TopRated', pageSize: 4 }).pipe(map((r) => r.items.filter((p) => p.rating.count > 0))),
  });
  protected readonly products = rxResource({ stream: () => this.catalog.products({ pageSize: 8 }) });
  protected readonly stores = rxResource({ stream: () => this.catalog.stores({ sort: 'TopRated', pageSize: 4 }) });

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.set('');
    });
  }
}
