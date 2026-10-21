import { Component, effect, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogApi } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { Seo } from '../../core/seo/seo';
import { ProductCard } from '../../shared/catalog/product-card';
import { StoreCard } from '../../shared/catalog/store-card';
import { LocalizedPipe } from '../../shared/i18n/localized';

/** Home: store categories, newest products, stores. "Top-rated" sections come with the ratings (017). */
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
  protected readonly products = rxResource({ stream: () => this.catalog.products({ pageSize: 8 }) });
  protected readonly stores = rxResource({ stream: () => this.catalog.stores({ pageSize: 4 }) });

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.set('');
    });
  }
}
