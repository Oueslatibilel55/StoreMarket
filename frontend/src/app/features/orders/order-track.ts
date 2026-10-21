import { Component, effect, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../core/http/problem';
import { LanguageService } from '../../core/i18n/language.service';
import { Order, OrderApi } from '../../core/orders/order-api';
import { Seo } from '../../core/seo/seo';
import { OrderView } from './order-view';

/** `/orders/track` — anyone (a guest especially) finds an order with its reference and the phone given at checkout. */
@Component({
  selector: 'app-order-track',
  imports: [ReactiveFormsModule, TranslocoPipe, OrderView],
  template: `
    <main class="page animate-fade-in">
      <div class="mx-auto max-w-xl text-center">
        <h1 class="page-title">{{ 'orders.track.title' | transloco }}</h1>
        <p class="page-subtitle">{{ 'orders.track.hint' | transloco }}</p>
      </div>
      <form [formGroup]="form" (ngSubmit)="find()" novalidate class="card card-body mx-auto mt-6 grid max-w-xl gap-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
        <div>
          <label for="reference" class="field-label">{{ 'orders.reference' | transloco }}</label>
          <input id="reference" formControlName="reference" placeholder="CMD-2026-000123" dir="ltr" class="mt-1.5 field font-mono uppercase" />
        </div>
        <div>
          <label for="trackPhone" class="field-label">{{ 'auth.phone' | transloco }}</label>
          <input id="trackPhone" type="tel" formControlName="phone" placeholder="22 123 456" dir="ltr" class="mt-1.5 field" />
        </div>
        <button type="submit" [disabled]="busy()" class="btn btn-primary">{{ 'orders.track.find' | transloco }}</button>
      </form>
      @if (error(); as key) {
        <p role="alert" class="alert-danger mx-auto mt-4 max-w-xl">{{ key | transloco }}</p>
      }
      @if (order(); as o) {
        <div class="mt-8"><app-order-view [order]="o" /></div>
      }
    </main>
  `,
})
export class OrderTrack {
  private readonly api = inject(OrderApi);
  protected readonly form = inject(NonNullableFormBuilder).group({
    reference: ['', Validators.required],
    phone: ['', Validators.required],
  });
  protected readonly order = signal<Order | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  constructor() {
    const seo = inject(Seo);
    const language = inject(LanguageService);
    effect(() => {
      language.lang();
      seo.setKey('orders.track.title');
    });
  }

  protected find(): void {
    if (this.form.invalid) return;
    const { reference, phone } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.api.lookup(reference.trim(), phone).subscribe({
      next: (order) => {
        this.order.set(order);
        this.busy.set(false);
      },
      error: (err: unknown) => {
        this.order.set(null);
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
