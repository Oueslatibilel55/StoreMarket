import { Component, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { StorePayment } from '../../core/billing/billing-api';
import { Modal } from '../ui/modal';

/** Asks the admin why a declared payment is refused (the owner sees the reason). */
@Component({
  selector: 'app-reject-dialog',
  imports: [TranslocoPipe, Modal],
  template: `
    <app-modal [label]="'billing.payments.reject' | transloco" (close)="cancelled.emit()">
      <h2 class="section-title">{{ 'billing.reject.title' | transloco: { number: payment().number } }}</h2>
      <label class="mt-4 block">
        <span class="field-label">{{ 'billing.reject.reason' | transloco }}</span>
        <textarea class="field" rows="3" maxlength="500" [value]="reason()" (input)="reason.set($any($event.target).value)"
          [placeholder]="'billing.reject.placeholder' | transloco"></textarea>
      </label>
      @if (error()) { <p class="alert-danger mt-3">{{ error()! | transloco }}</p> }
      <div class="mt-5 flex justify-end gap-2">
        <button type="button" class="btn btn-ghost" (click)="cancelled.emit()">{{ 'common.cancel' | transloco }}</button>
        <button type="button" class="btn btn-danger" [disabled]="busy() || !reason().trim()" (click)="confirmed.emit(reason().trim())">
          {{ 'billing.payments.reject' | transloco }}
        </button>
      </div>
    </app-modal>
  `,
})
export class RejectDialog {
  readonly payment = input.required<StorePayment>();
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly confirmed = output<string>();
  readonly cancelled = output<void>();

  protected readonly reason = signal('');
}
