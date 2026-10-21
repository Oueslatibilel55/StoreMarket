import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { finalize, Observable, of, ReplaySubject, share, tap } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { Cart, CartApi, CartLine, EMPTY_CART, GuestLine } from './cart-api';

const STORAGE_KEY = 'webapp.cart';
const MAX_QUANTITY = 99;

/** Normalised like the server (`#drf45` → `DRF-45`) so a guest line keeps one code per product. */
export function normalizeCode(raw: string | null | undefined): string | null {
  const compact = (raw ?? '').replace(/[^a-z0-9]/gi, '').toUpperCase();
  const match = /^([A-Z]{2,5})([0-9]{2,6})$/.exec(compact);
  return match ? `${match[1]}-${match[2]}` : null;
}

/**
 * The cart (013). Signed in: kept on the server. Guest: kept in this browser (localStorage) and shown
 * through the preview endpoint, then merged into the account at sign-in. Pages read `cart()` and `count()`;
 * every change returns the fresh cart (prices, stock and codes checked by the server).
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly api = inject(CartApi);
  private readonly auth = inject(AuthService);

  private readonly state = signal<Cart>(EMPTY_CART);
  readonly cart = this.state.asReadonly();
  readonly count = computed(() => this.state().itemCount);
  readonly loading = signal(false);

  private readonly userId = computed(() => this.auth.user()?.id ?? null);

  constructor() {
    // Sign-in (or session restored): the guest cart joins the account's. Sign-out: back to the browser cart.
    effect(() => {
      const userId = this.userId();
      untracked(() => (userId ? this.mergeGuest() : this.showGuest()));
    });
  }

  /** Reloads the cart (after coming back to the page, for instance). */
  refresh(): void {
    this.run(this.userId() ? this.api.get() : this.previewGuest());
  }

  add(line: GuestLine): Observable<Cart> {
    if (this.userId()) return this.run(this.api.add(line));
    const lines = this.guestLines();
    const existing = lines.find((l) => l.productId === line.productId && l.variantId === line.variantId);
    if (existing) {
      existing.quantity = Math.min(existing.quantity + line.quantity, MAX_QUANTITY);
      existing.affiliateCode = normalizeCode(line.affiliateCode) ?? existing.affiliateCode;
    } else {
      lines.push({ ...line, affiliateCode: normalizeCode(line.affiliateCode) });
    }
    this.saveGuest(lines);
    return this.run(this.previewGuest());
  }

  setQuantity(line: CartLine, quantity: number): Observable<Cart> {
    if (line.id) return this.run(this.api.setQuantity(line.id, quantity));
    return this.changeGuest(line, (l) => (l.quantity = Math.max(1, Math.min(quantity, MAX_QUANTITY))));
  }

  /** Null removes the code. A guest's malformed code is refused here, like the server would. */
  setCode(line: CartLine, code: string | null): Observable<Cart> {
    if (line.id) return this.run(this.api.setCode(line.id, code));
    return this.changeGuest(line, (l) => (l.affiliateCode = normalizeCode(code)));
  }

  remove(line: CartLine): Observable<Cart> {
    if (line.id) return this.run(this.api.remove(line.id));
    this.saveGuest(this.guestLines().filter((l) => !same(l, line)));
    return this.run(this.previewGuest());
  }

  /** What checkout sends: null when signed in (the server orders its own cart), the browser cart for a guest. */
  checkoutLines(): GuestLine[] | null {
    return this.userId() ? null : this.guestLines();
  }

  /** The order is placed: the server already emptied a signed-in cart; a guest's browser cart goes too. */
  ordered(): void {
    if (!this.userId()) this.saveGuest([]);
    this.state.set(EMPTY_CART);
  }

  clear(): Observable<Cart> {
    if (this.userId()) return this.run(this.api.clear());
    this.saveGuest([]);
    this.state.set(EMPTY_CART);
    return of(EMPTY_CART);
  }

  // -- Guest cart -------------------------------------------------------------------

  private mergeGuest(): void {
    const lines = this.guestLines();
    this.run(lines.length ? this.api.merge(lines) : this.api.get()).subscribe({
      next: () => this.saveGuest([]),
      error: () => undefined,
    });
  }

  private showGuest(): void {
    this.state.set(EMPTY_CART);
    if (this.guestLines().length) this.run(this.previewGuest()).subscribe({ error: () => undefined });
  }

  private previewGuest(): Observable<Cart> {
    const lines = this.guestLines();
    return lines.length ? this.api.preview(lines) : of(EMPTY_CART);
  }

  private changeGuest(line: CartLine, change: (l: GuestLine) => void): Observable<Cart> {
    const lines = this.guestLines();
    const target = lines.find((l) => same(l, line));
    if (target) change(target);
    this.saveGuest(lines);
    return this.run(this.previewGuest());
  }

  private guestLines(): GuestLine[] {
    try {
      const raw = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]');
      return Array.isArray(raw) ? raw : [];
    } catch {
      return []; // storage blocked or corrupt: start an empty cart
    }
  }

  private saveGuest(lines: GuestLine[]): void {
    try {
      if (lines.length) localStorage.setItem(STORAGE_KEY, JSON.stringify(lines));
      else localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage blocked (private mode…): the cart lives for this page only.
    }
  }

  /** Runs the call once (callers may or may not subscribe), stores the fresh cart, replays the result. */
  private run(call: Observable<Cart>): Observable<Cart> {
    this.loading.set(true);
    const shared = call.pipe(
      tap((cart) => this.state.set(cart)),
      finalize(() => this.loading.set(false)),
      share({ connector: () => new ReplaySubject<Cart>(1), resetOnError: false, resetOnComplete: false, resetOnRefCountZero: false }),
    );
    shared.subscribe({ error: () => undefined });
    return shared;
  }

}

function same(a: Pick<GuestLine, 'productId' | 'variantId'>, b: Pick<GuestLine, 'productId' | 'variantId'>): boolean {
  return a.productId === b.productId && a.variantId === b.variantId;
}
