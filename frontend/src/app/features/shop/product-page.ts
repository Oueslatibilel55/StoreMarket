import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { CartApi } from '../../core/cart/cart-api';
import { CartService } from '../../core/cart/cart.service';
import { Referrals } from '../../core/cart/referrals';
import { errorKey } from '../../core/http/problem';
import { CatalogApi, PublicAttribute } from '../../core/catalog/catalog-api';
import { LanguageService } from '../../core/i18n/language.service';
import { Seo } from '../../core/seo/seo';
import { PlatformSettings } from '../../core/settings/platform-settings';
import { coinsPerItem } from '../../core/stores/product-api';
import { LocalizedPipe, pickName } from '../../shared/i18n/localized';
import { PartnershipCta } from '../../shared/partnerships/partnership-cta';
import { ReviewSection } from '../../shared/reviews/review-section';
import { StarRating } from '../../shared/reviews/star-rating';
import { optionExists, selectedVariant } from './variant-picker';

/** `/products/:slug` — gallery, price, variant choice, details, store, reviews; affiliates see their code and the coins they'd earn. */
@Component({
  selector: 'app-product-page',
  imports: [DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, PartnershipCta, ReviewSection, StarRating],
  templateUrl: './product-page.html',
})
export class ProductPage {
  private readonly api = inject(CatalogApi);
  private readonly settings = inject(PlatformSettings);
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);

  readonly slug = input.required<string>();
  /** Affiliate share link `?ref=DRF-45` (012): remembered for this store, used when adding to the cart. */
  readonly ref = input<string | undefined>();

  protected readonly product = rxResource({ params: () => this.slug(), stream: ({ params }) => this.api.product(params) });
  protected readonly store = rxResource({
    params: () => this.product.value()?.store.slug,
    stream: ({ params }) => this.api.store(params),
  });

  protected readonly imageIndex = signal(0);
  protected readonly selection = signal<Record<string, string>>({});
  protected readonly copied = signal(false);

  private readonly cart = inject(CartService);
  private readonly cartApi = inject(CartApi);
  private readonly referrals = inject(Referrals);
  protected readonly quantity = signal(1);
  protected readonly adding = signal(false);
  protected readonly added = signal(false);
  protected readonly addError = signal<string | null>(null);
  /** "Sana T." when the share link's code is valid on this product. */
  protected readonly recommendedBy = signal<string | null>(null);

  private readonly axes = computed(() => this.product.value()?.variantAxes.map((a) => a.key) ?? []);
  protected readonly variant = computed(() => {
    const p = this.product.value();
    return p ? selectedVariant(p.variants, this.axes(), this.selection()) : null;
  });
  protected readonly price = computed(() => this.variant()?.price ?? this.product.value()?.price ?? 0);
  /** "From …" until a variant is chosen, when variants have different prices. */
  protected readonly showFrom = computed(() => {
    const p = this.product.value();
    return !!p && !this.variant() && p.minPrice < p.price;
  });
  protected readonly inStock = computed(() => {
    const p = this.product.value();
    if (!p) return false;
    const v = this.variant();
    return p.variants.length ? (v ? v.inStock : p.inStock) : p.inStock;
  });
  protected readonly coins = computed(() => {
    const p = this.product.value();
    return p ? coinsPerItem(this.price(), p.affiliatePercent, this.settings.number('coin.valueInTnd', 1)) : 0;
  });

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      const p = this.product.value();
      if (p) seo.set(p.name, p.description);
    });
    // A new product: back to the first photo, nothing chosen.
    effect(() => {
      this.slug();
      this.imageIndex.set(0);
      this.selection.set({});
      this.quantity.set(1);
      this.added.set(false);
      this.addError.set(null);
    });
    // A share link: keep the code for this store, and say who recommended the product when the code counts here.
    effect(() => {
      const p = this.product.value();
      const ref = this.ref();
      this.recommendedBy.set(null);
      if (!p || !ref) return;
      this.referrals.remember(p.store.id, ref);
      this.cartApi.checkCode(ref, p.id).subscribe({
        next: (check) => this.recommendedBy.set(check.affiliateName),
        error: () => undefined, // an invalid code is reported on the cart line instead
      });
    });
  }

  /** In stock, a combination chosen when there are variants, and a store taking orders. */
  protected readonly canAdd = computed(() => {
    const p = this.product.value();
    return !!p && p.store.isOpen && this.inStock() && (!p.variants.length || !!this.variant());
  });

  protected addToCart(): void {
    const p = this.product.value();
    if (!p || !this.canAdd()) return;
    this.adding.set(true);
    this.added.set(false);
    this.addError.set(null);
    this.cart.add({
      productId: p.id,
      variantId: this.variant()?.id ?? null,
      quantity: this.quantity(),
      affiliateCode: this.referrals.codeFor(p.store.id),
    }).subscribe({
      next: () => {
        this.adding.set(false);
        this.added.set(true);
        this.quantity.set(1);
      },
      error: (err: unknown) => {
        this.adding.set(false);
        this.addError.set(errorKey(err));
      },
    });
  }

  protected choose(axis: string, option: string): void {
    this.selection.update((s) => (s[axis] === option ? Object.fromEntries(Object.entries(s).filter(([k]) => k !== axis)) : { ...s, [axis]: option }));
  }

  protected exists(axis: string, option: string): boolean {
    const p = this.product.value();
    return !!p && optionExists(p.variants, this.axes(), this.selection(), axis, option);
  }

  protected display(a: PublicAttribute): string {
    const lang = this.language.lang();
    switch (a.type) {
      case 'Number': return `${a.number?.toLocaleString(this.language.locale())}${a.unit ? ' ' + a.unit : ''}`;
      case 'Boolean': return a.boolean ? '✓' : '✗';
      case 'SingleChoice':
      case 'MultiChoice': return a.options.map((o) => pickName(o.name, lang)).join(', ');
      default: return a.text ?? '';
    }
  }

  protected scrollToReviews(): void {
    document.getElementById('reviews')?.scrollIntoView({ behavior: 'smooth' });
  }

  protected async copyCode(code: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(`#${code}`);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      // Clipboard blocked: the code stays visible to copy by hand.
    }
  }
}
