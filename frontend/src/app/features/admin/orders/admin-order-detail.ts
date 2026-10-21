import { Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../../core/http/problem';
import { NotDeliveredReason, StoreOrder, StoreOrderStatus } from '../../../core/orders/order-api';
import { Modal } from '../../../shared/ui/modal';
import { OrderView } from '../../orders/order-view';
import { AdminApi } from '../admin-api';

/**
 * `/admin/orders/:reference` — an order with every store's history. The admin can force a store's status
 * (dispute, store not answering…): reason required, audited; Delivered stays final.
 */
@Component({
  selector: 'app-admin-order-detail',
  imports: [RouterLink, TranslocoPipe, Modal, OrderView],
  template: `
    <a routerLink="/admin/orders" class="btn btn-ghost btn-sm -ms-3"><span aria-hidden="true" class="inline-block rtl:rotate-180">←</span> {{ 'adminOrders.title' | transloco }}</a>
    <h1 class="page-title mt-2">{{ 'orders.order' | transloco }} <span dir="ltr">{{ reference() }}</span></h1>

    @if (order.value(); as o) {
      <section class="card card-body mt-6">
        <h2 class="section-title">{{ 'adminOrders.force' | transloco }}</h2>
        <p class="mt-1 text-sm text-muted">{{ 'adminOrders.forceHint' | transloco }}</p>
        <div class="mt-4 flex flex-wrap gap-2">
          @for (s of o.storeOrders; track s.id) {
            <button type="button" (click)="ask(s)" [disabled]="s.status === 'Delivered'" class="btn btn-secondary btn-sm">
              {{ s.storeName }}…
            </button>
          }
        </div>
      </section>
      <div class="mt-6"><app-order-view [order]="o" /></div>
    } @else if (order.error()) {
      <div class="card empty-state mt-6"><p>{{ 'errors.order.notFound' | transloco }}</p></div>
    } @else {
      <p class="card empty-state mt-6">{{ 'common.loading' | transloco }}</p>
    }

    @if (target(); as s) {
      <app-modal [label]="'adminOrders.force' | transloco" (close)="close()">
        <h2 class="section-title">{{ 'adminOrders.forceTitle' | transloco: { store: s.storeName } }}</h2>
        <p class="mt-1 text-sm text-muted">{{ 'adminOrders.current' | transloco }} <strong>{{ 'orders.status.' + s.status | transloco }}</strong></p>
        <label for="forceStatus" class="field-label mt-4">{{ 'adminOrders.newStatus' | transloco }}</label>
        <select id="forceStatus" class="mt-1.5 field" [value]="status() ?? ''" (change)="status.set($any($event.target).value || null)">
          <option value="" disabled>{{ 'common.choose' | transloco }}</option>
          @for (st of statuses; track st) {
            @if (st !== s.status) { <option [value]="st">{{ 'orders.status.' + st | transloco }}</option> }
          }
        </select>
        @if (status() === 'NotDelivered') {
          <label for="forceNd" class="field-label mt-4">{{ 'storeOrders.whyNotDelivered' | transloco }}</label>
          <select id="forceNd" class="mt-1.5 field" [value]="notDelivered() ?? ''" (change)="notDelivered.set($any($event.target).value || null)">
            <option value="" disabled>{{ 'common.choose' | transloco }}</option>
            @for (r of reasons; track r) { <option [value]="r">{{ 'orders.notDelivered.' + r | transloco }}</option> }
          </select>
        }
        <label for="forceReason" class="field-label mt-4">{{ 'adminOrders.reason' | transloco }}</label>
        <textarea id="forceReason" rows="3" maxlength="500" [value]="reason()" (input)="reason.set($any($event.target).value)" class="mt-1.5 field resize-y"></textarea>
        @if (error(); as key) { <p role="alert" class="alert-danger mt-4">{{ key | transloco }}</p> }
        <div class="mt-6 flex justify-end gap-3">
          <button type="button" (click)="close()" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
          <button type="button" (click)="confirm()" [disabled]="busy() || !ready()" class="btn btn-danger">{{ 'adminOrders.apply' | transloco }}</button>
        </div>
      </app-modal>
    }
  `,
})
export class AdminOrderDetail {
  private readonly api = inject(AdminApi);

  readonly reference = input.required<string>();
  protected readonly order = rxResource({ params: () => this.reference(), stream: ({ params }) => this.api.order(params) });

  protected readonly statuses: StoreOrderStatus[] = ['Placed', 'Confirmed', 'Shipped', 'Delivered', 'NotDelivered', 'Cancelled'];
  protected readonly reasons: NotDeliveredReason[] = ['Refused', 'Unreachable', 'WrongAddress', 'Other'];
  protected readonly target = signal<StoreOrder | null>(null);
  protected readonly status = signal<StoreOrderStatus | null>(null);
  protected readonly notDelivered = signal<NotDeliveredReason | null>(null);
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected ask(storeOrder: StoreOrder): void {
    this.status.set(null);
    this.notDelivered.set(null);
    this.reason.set('');
    this.error.set(null);
    this.target.set(storeOrder);
  }

  protected close(): void {
    if (!this.busy()) this.target.set(null);
  }

  protected ready(): boolean {
    return !!this.status() && !!this.reason().trim() && (this.status() !== 'NotDelivered' || !!this.notDelivered());
  }

  protected confirm(): void {
    const target = this.target();
    const status = this.status();
    if (!target || !status || !this.ready()) return;
    this.busy.set(true);
    this.api.forceStatus(target.id, status, this.reason().trim(), this.notDelivered()).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.busy.set(false);
        this.target.set(null);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
