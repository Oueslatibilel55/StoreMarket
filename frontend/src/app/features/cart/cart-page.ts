import { DecimalPipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { Cart, CartLine } from '../../core/cart/cart-api';
import { CartService, normalizeCode } from '../../core/cart/cart.service';
import { errorKey } from '../../core/http/problem';
import { LanguageService } from '../../core/i18n/language.service';
import { Seo } from '../../core/seo/seo';
import { LocalizedPipe } from '../../shared/i18n/localized';

/** Line key: server id, or product + variant for a guest cart. */
const keyOf = (l: CartLine) => l.id ?? `${l.productId}:${l.variantId ?? ''}`;

/**
 * `/cart` — the cart grouped by store (013): quantity, an affiliate code per line, store subtotal +
 * delivery, then the total. Checkout comes with 014.
 */
@Component({
  selector: 'app-cart-page',
  imports: [DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe],
  templateUrl: './cart-page.html',
})
export class CartPage {
  protected readonly cart = inject(CartService);
  protected readonly language = inject(LanguageService);

  protected readonly keyOf = keyOf;
  /** Line whose code field is open, and what is typed in it. */
  protected readonly editingCode = signal<string | null>(null);
  protected readonly codeDraft = signal('');
  /** Errors are shown on the line they belong to. */
  protected readonly lineError = signal<{ key: string; error: string } | null>(null);
  protected readonly busyKey = signal<string | null>(null);

  constructor() {
    this.cart.refresh();
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('cart.title');
    });
  }

  protected quantity(line: CartLine, quantity: number): void {
    if (quantity < 1 || quantity === line.quantity) return;
    this.run(line, this.cart.setQuantity(line, quantity));
  }

  protected remove(line: CartLine): void {
    this.run(line, this.cart.remove(line));
  }

  protected openCode(line: CartLine): void {
    this.lineError.set(null);
    this.codeDraft.set(line.affiliateCode ? `#${line.affiliateCode}` : '');
    this.editingCode.set(keyOf(line));
    // The field appears on the next render: put the cursor in it.
    setTimeout(() => (document.getElementById('cart-code-field') as HTMLInputElement | null)?.focus());
  }

  protected applyCode(line: CartLine): void {
    const draft = this.codeDraft().trim();
    if (draft && !normalizeCode(draft)) {
      this.lineError.set({ key: keyOf(line), error: 'errors.cart.codeInvalid' });
      return;
    }
    this.run(line, this.cart.setCode(line, draft || null), () => this.editingCode.set(null));
  }

  protected removeCode(line: CartLine): void {
    this.run(line, this.cart.setCode(line, null));
  }

  protected issueParams(line: CartLine): Record<string, number> {
    return { count: line.available };
  }

  private run(line: CartLine, call: Observable<Cart>, done?: () => void): void {
    const key = keyOf(line);
    this.busyKey.set(key);
    this.lineError.set(null);
    call.subscribe({
      next: () => {
        this.busyKey.set(null);
        done?.();
      },
      error: (err: unknown) => {
        this.busyKey.set(null);
        this.lineError.set({ key, error: errorKey(err) });
      },
    });
  }
}
