import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { errorKey } from '../../core/http/problem';
import { Order, OrderApi, StoreOrder } from '../../core/orders/order-api';
import { Modal } from '../../shared/ui/modal';
import { Seo } from '../../core/seo/seo';
import { OrderView } from './order-view';

/**
 * `/orders/:reference` — one order. Signed in: loaded from "My orders". A guest arrives here right after
 * checkout with the order in the navigation state; later they find it again through `/orders/track`.
 */
@Component({
  selector: 'app-order-page',
  imports: [RouterLink, TranslocoPipe, OrderView, Modal],
  template: `
    <main class="page animate-fade-in">
      @if (placed()) {
        <div class="card mb-8 flex flex-col items-center gap-3 border-t-4 border-t-success px-5 py-10 text-center sm:px-8">
          <span class="flex size-12 items-center justify-center rounded-full bg-success/10 text-xl font-bold text-success ring-1 ring-success/20" aria-hidden="true">✓</span>
          <h1 class="page-title">{{ 'orders.thanks' | transloco }}</h1>
          <p class="max-w-xl text-ink-soft">{{ 'orders.thanksHint' | transloco }}</p>
          <p class="flex flex-wrap items-center justify-center gap-2 text-sm text-muted">{{ 'orders.reference' | transloco }} <span class="code-chip text-base" dir="ltr">{{ reference() }}</span></p>
          @if (!auth.isAuthenticated()) {
            <p class="text-xs text-muted">{{ 'orders.keepReference' | transloco }}</p>
          }
        </div>
      } @else {
        <a [routerLink]="auth.isAuthenticated() ? '/orders' : '/orders/track'" class="back-link">
          <span aria-hidden="true" class="inline-block rtl:rotate-180">←</span> {{ 'orders.back' | transloco }}
        </a>
        <header class="page-header">
          <div class="min-w-0">
            <h1 class="page-title flex flex-wrap items-center gap-x-3 gap-y-1">{{ 'orders.order' | transloco }} <span class="code-chip text-lg" dir="ltr">{{ reference() }}</span></h1>
          </div>
        </header>
      }

      @if (order(); as o) {
        <app-order-view [order]="o" [cancellable]="auth.isAuthenticated()" (cancel)="askCancel($event)" />
        @if (placed()) {
          <div class="mt-8 flex flex-wrap justify-center gap-3">
            <a routerLink="/products" class="btn btn-secondary">{{ 'cart.continue' | transloco }}</a>
            @if (auth.isAuthenticated()) { <a routerLink="/orders" class="btn btn-primary">{{ 'orders.mine' | transloco }}</a> }
          </div>
        }
      } @else if (loaded.error() || (!loaded.isLoading() && !order())) {
        <div class="card empty-state">
          <p>{{ 'errors.order.notFound' | transloco }}</p>
          <a routerLink="/orders/track" class="btn btn-secondary mt-3">{{ 'orders.track.title' | transloco }}</a>
        </div>
      } @else {
        <p class="card empty-state">{{ 'common.loading' | transloco }}</p>
      }
    </main>

    @if (cancelling(); as s) {
      <app-modal [label]="'orders.cancel' | transloco" (close)="closeCancel()">
        <h2 class="section-title">{{ 'orders.cancelTitle' | transloco: { store: s.storeName } }}</h2>
        <p class="mt-2 text-sm text-muted">{{ 'orders.cancelHint' | transloco }}</p>
        <label for="cancelReason" class="field-label mt-4">{{ 'orders.cancelReason' | transloco }}</label>
        <textarea id="cancelReason" rows="3" maxlength="500" [value]="reason()" (input)="reason.set($any($event.target).value)" class="mt-1.5 field resize-y"></textarea>
        @if (error(); as key) { <p role="alert" class="alert-danger mt-4">{{ key | transloco }}</p> }
        <div class="mt-6 flex flex-wrap justify-end gap-3">
          <button type="button" (click)="closeCancel()" class="btn btn-ghost">{{ 'orders.keep' | transloco }}</button>
          <button type="button" (click)="confirmCancel()" [disabled]="busy()" class="btn btn-danger">{{ 'orders.cancel' | transloco }}</button>
        </div>
      </app-modal>
    }
  `,
})
export class OrderPage {
  private readonly api = inject(OrderApi);
  protected readonly auth = inject(AuthService);

  readonly reference = input.required<string>();
  /** `?placed=1` right after checkout: show the thank-you banner. */
  readonly placed = input(false, { transform: (v: unknown) => v === '1' || v === 1 || v === true });

  /** Handed over by checkout (works for guests too). */
  private readonly fromCheckout = signal<Order | null>((inject(Router).getCurrentNavigation()?.extras.state?.['order'] as Order | undefined) ?? null);
  protected readonly loaded = rxResource({
    params: () => (this.fromCheckout() ? undefined : this.auth.isAuthenticated() ? this.reference() : undefined),
    stream: ({ params }) => (params ? this.api.get(params) : of(null)),
  });
  protected readonly order = computed(() => this.fromCheckout() ?? this.loaded.value() ?? null);

  protected readonly cancelling = signal<StoreOrder | null>(null);
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected askCancel(storeOrder: StoreOrder): void {
    this.reason.set('');
    this.error.set(null);
    this.cancelling.set(storeOrder);
  }

  protected closeCancel(): void {
    if (!this.busy()) this.cancelling.set(null);
  }

  protected confirmCancel(): void {
    const storeOrder = this.cancelling();
    const order = this.order();
    if (!storeOrder || !order) return;
    this.busy.set(true);
    this.api.cancel(order.reference, storeOrder.id, this.reason().trim() || null).subscribe({
      next: (updated) => {
        this.fromCheckout.set(updated);
        this.busy.set(false);
        this.cancelling.set(null);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }

  constructor() {
    const seo = inject(Seo);
    const language = inject(LanguageService);
    effect(() => {
      language.lang();
      seo.setKey('orders.order');
    });
  }
}
