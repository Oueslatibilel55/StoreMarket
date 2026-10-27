import { DatePipe } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { orderStatusTone, StoreOrderEvent } from '../../core/orders/order-api';

/** The status history of one store order (016): newest first, who changed it and why. */
@Component({
  selector: 'app-order-timeline',
  imports: [DatePipe, TranslocoPipe],
  template: `
    @if (history().length) {
      <ol class="space-y-3 border-s-2 border-line ps-4">
        @for (e of reversed(); track $index) {
          <li class="relative text-sm">
            <span class="absolute -start-[1.4rem] top-1.5 size-2.5 rounded-full ring-4 ring-card"
              [class]="e.to === 'Delivered' ? 'bg-success' : e.to === 'Cancelled' || e.to === 'NotDelivered' ? 'bg-danger' : 'bg-brand'" aria-hidden="true"></span>
            <p class="flex flex-wrap items-center gap-2">
              <span class="pill" [class]="tone(e.to)">{{ 'orders.status.' + e.to | transloco }}</span>
              <span class="text-xs text-muted">{{ e.at | date: 'medium' : undefined : language.locale() }}</span>
              <span class="text-xs font-medium text-ink-soft">{{ 'orders.actor.' + e.actorRole | transloco }}</span>
            </p>
            @if (e.notDeliveredReason) {
              <p class="mt-1 text-xs font-medium text-danger">{{ 'orders.notDelivered.' + e.notDeliveredReason | transloco }}</p>
            }
            @if (e.reason) { <p class="mt-1 rounded-lg bg-subtle px-3 py-1.5 text-xs text-ink-soft">{{ e.reason }}</p> }
          </li>
        }
      </ol>
    }
  `,
})
export class OrderTimeline {
  readonly history = input.required<StoreOrderEvent[]>();
  protected readonly language = inject(LanguageService);
  protected readonly tone = orderStatusTone;
  protected readonly reversed = () => [...this.history()].reverse();
}
