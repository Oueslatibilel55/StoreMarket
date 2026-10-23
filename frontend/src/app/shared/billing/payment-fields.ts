import { Component, input, model } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { PAYMENT_METHODS, StorePaymentMethod } from '../../core/billing/billing-api';

/** How and when a store paid — the part every payment form shares (025). Two-way bound: `[(method)]`, `[(reference)]`… */
@Component({
  selector: 'app-payment-fields',
  imports: [TranslocoPipe],
  template: `
    <div class="grid gap-3 sm:grid-cols-2">
      <label class="block">
        <span class="field-label">{{ 'billing.fields.method' | transloco }}</span>
        <select class="field" [value]="method()" (change)="method.set($any($event.target).value)">
          @for (m of methods; track m) { <option [value]="m" [selected]="m === method()">{{ 'billing.method.' + m | transloco }}</option> }
        </select>
      </label>
      <label class="block">
        <span class="field-label">{{ 'billing.fields.paidOn' | transloco }}</span>
        <input type="date" class="field" dir="ltr" [value]="paidOn()" [max]="today" (change)="paidOn.set($any($event.target).value)" />
      </label>
      <label class="block sm:col-span-2">
        <span class="field-label">{{ 'billing.fields.reference' | transloco }}</span>
        <input type="text" class="field" maxlength="120" [value]="reference()" (input)="reference.set($any($event.target).value)"
          [placeholder]="(method() === 'BankTransfer' ? 'billing.fields.referenceTransfer' : 'billing.fields.referenceCash') | transloco" />
      </label>
      @if (withNote()) {
        <label class="block sm:col-span-2">
          <span class="field-label">{{ 'billing.fields.note' | transloco }}</span>
          <textarea class="field" rows="2" maxlength="500" [value]="note()" (input)="note.set($any($event.target).value)"></textarea>
        </label>
      }
    </div>
  `,
})
export class PaymentFields {
  readonly method = model<StorePaymentMethod>('Cash');
  readonly reference = model('');
  readonly note = model('');
  readonly paidOn = model(new Date().toISOString().slice(0, 10));
  readonly withNote = input(true);

  protected readonly methods = PAYMENT_METHODS;
  protected readonly today = new Date().toISOString().slice(0, 10);
}
