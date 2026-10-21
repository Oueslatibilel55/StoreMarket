import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { PayoutEvent, payoutTone } from '../../core/payouts/payout-api';

/** The steps of one payout request (021): newest first, with who did it (admin view) and the note / reason / place. */
@Component({
  selector: 'app-payout-timeline',
  imports: [DatePipe, TranslocoPipe],
  template: `
    <ol class="space-y-3 border-s-2 border-line ps-4">
      @for (e of reversed(); track $index) {
        <li class="relative text-sm">
          <span class="absolute -start-[1.4rem] top-1.5 size-2.5 rounded-full ring-4 ring-card"
            [class]="e.status === 'Paid' ? 'bg-success' : e.status === 'Rejected' ? 'bg-danger' : e.status === 'Cancelled' ? 'bg-muted' : 'bg-brand'" aria-hidden="true"></span>
          <p class="flex flex-wrap items-center gap-2">
            <span class="pill" [class]="tone(e.status)">{{ 'payouts.status.' + e.status | transloco }}</span>
            <span class="text-xs text-muted">{{ e.at | date: 'medium' : undefined : language.locale() }} ·
              {{ e.byAffiliate ? (affiliateName() ?? ('payouts.actor.affiliate' | transloco)) : (e.actorName ?? ('payouts.actor.team' | transloco)) }}</span>
          </p>
          @if (e.note) { <p class="mt-1 rounded-lg bg-subtle px-3 py-1.5 text-xs text-ink-soft">{{ e.note }}</p> }
        </li>
      }
    </ol>
  `,
})
export class PayoutTimeline {
  readonly history = input.required<PayoutEvent[]>();
  /** Admin view: the affiliate's name instead of "You" on their own steps. */
  readonly affiliateName = input<string | null>(null);
  protected readonly language = inject(LanguageService);
  protected readonly tone = payoutTone;
  protected readonly reversed = computed(() => [...this.history()].reverse());
}
