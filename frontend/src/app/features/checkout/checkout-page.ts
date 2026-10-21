import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { CartService } from '../../core/cart/cart.service';
import { errorKey } from '../../core/http/problem';
import { LanguageService } from '../../core/i18n/language.service';
import { OrderApi } from '../../core/orders/order-api';
import { Seo } from '../../core/seo/seo';
import { CITIES } from '../../core/stores/cities';

/** Errors that mean "the cart changed": send the client back to it, where each line says why. */
const CART_ERRORS = ['errors.checkout.cartNotReady', 'errors.checkout.codeRefused', 'errors.cart.notEnoughStock', 'errors.checkout.cartEmpty'];

/**
 * `/checkout` — delivery details and a last look at the order (014). Cash on delivery only. An affiliate can
 * order for a client: their code then goes on every product of the stores they work with.
 */
@Component({
  selector: 'app-checkout-page',
  imports: [DecimalPipe, ReactiveFormsModule, RouterLink, TranslocoPipe],
  templateUrl: './checkout-page.html',
})
export class CheckoutPage {
  protected readonly cart = inject(CartService);
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  private readonly orders = inject(OrderApi);
  private readonly router = inject(Router);

  protected readonly cities = CITIES;
  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    phone: ['', [Validators.required, Validators.pattern(/^(\+216|00216)?[\s.-]*(\d[\s.-]*){8}$/)]],
    city: ['', Validators.required],
    address: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(300)]],
    note: ['', Validators.maxLength(500)],
  });

  /** Affiliate mode: ordering for a client. */
  protected readonly forClient = signal(false);
  protected readonly canOrderForClient = computed(() => this.auth.hasRole('Affiliate') && !!this.auth.user()?.affiliateCode);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly cartError = computed(() => !!this.error() && CART_ERRORS.includes(this.error()!));

  /** Every line can be ordered. In affiliate mode the lines' codes don't matter: the server uses the affiliate's own. */
  protected readonly ready = computed(() => {
    const cart = this.cart.cart();
    return this.forClient() ? cart.stores.length > 0 && cart.stores.every((s) => s.lines.every((l) => !l.issue)) : cart.canCheckout;
  });

  private readonly city = signal('');
  /** Stores that don't deliver the chosen city. */
  protected readonly notDelivered = computed(() =>
    this.city() ? this.cart.cart().stores.filter((s) => !s.deliveryCities.includes(this.city())).map((s) => s.name) : []);

  constructor() {
    this.cart.refresh();
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('checkout.title');
    });
    this.form.controls.city.valueChanges.subscribe((c) => this.city.set(c));
    this.prefill();
  }

  protected setForClient(value: boolean): void {
    this.forClient.set(value);
    if (value) this.form.patchValue({ fullName: '', phone: '' });
    else this.prefill();
  }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.touched && control.invalid;
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.notDelivered().length || !this.ready()) return;

    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);
    this.orders.place({ ...value, note: value.note.trim() || null }, this.forClient(), this.cart.checkoutLines()).subscribe({
      next: (order) => {
        this.cart.ordered();
        this.router.navigate(['/orders', order.reference], { queryParams: { placed: 1 }, state: { order } });
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(errorKey(err));
        this.cart.refresh();
      },
    });
  }

  /** The signed-in client's name and number, as a starting point. */
  private prefill(): void {
    const user = this.auth.user();
    if (user) this.form.patchValue({ fullName: user.fullName, phone: user.phone.replace(/^\+216/, '') });
  }
}
