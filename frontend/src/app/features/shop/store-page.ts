import { DecimalPipe } from '@angular/common';
import { Component, effect, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogApi, ProductSort } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { Seo } from '../../core/seo/seo';
import { AuthService } from '../../core/auth/auth.service';
import { PartnershipCta } from '../../shared/partnerships/partnership-cta';
import { Referrals } from '../../core/cart/referrals';
import { ProductCard } from '../../shared/catalog/product-card';
import { LocalizedPipe } from '../../shared/i18n/localized';
import { Pagination } from '../../shared/ui/pagination';
import { ReviewSection } from '../../shared/reviews/review-section';
import { StarRating } from '../../shared/reviews/star-rating';
import { SORTS } from './product-catalog';

const PAGE_SIZE = 24;

/** `/stores/:slug?sort=&page=` — a store, its products and the reviews of its service. */
@Component({
  selector: 'app-store-page',
  imports: [DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, ProductCard, Pagination, PartnershipCta, ReviewSection, StarRating],
  templateUrl: './store-page.html',
})
export class StorePage {
  private readonly api = inject(CatalogApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);
  protected readonly auth = inject(AuthService);

  readonly slug = input.required<string>();
  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly sort = input<ProductSort | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });
  /** Affiliate share link `?ref=DRF-45`: remembered for this store's products (013). */
  readonly ref = input<string | undefined>();

  protected readonly sorts = SORTS;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly store = rxResource({ params: () => this.slug(), stream: ({ params }) => this.api.store(params) });
  protected readonly products = rxResource({
    params: () => ({ store: this.slug(), sort: this.sort(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.products(params),
  });

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      const s = this.store.value();
      if (s) seo.set(s.name, s.description);
    });
    const referrals = inject(Referrals);
    effect(() => {
      const s = this.store.value();
      if (s && this.ref()) referrals.remember(s.id, this.ref());
    });
  }

  protected scrollToReviews(): void {
    document.getElementById('reviews')?.scrollIntoView({ behavior: 'smooth' });
  }

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }
}
