import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';

/**
 * Read-only stars (017): five stars filled up to `value` (fractions shown), optionally the average and the count.
 * `<app-star-rating [value]="4.6" [count]="12" />` — nothing is drawn for a value of null.
 */
@Component({
  selector: 'app-star-rating',
  imports: [DecimalPipe, TranslocoPipe],
  template: `
    @if (value() !== null) {
      <span class="inline-flex items-center gap-1.5 align-middle" role="img"
        [attr.aria-label]="'reviews.starsLabel' | transloco: { value: (value() | number: '1.0-1' : language.locale()) }">
        <span class="relative inline-block leading-none tracking-tight" [class]="size() === 'lg' ? 'text-2xl' : size() === 'sm' ? 'text-sm' : 'text-base'" dir="ltr" aria-hidden="true">
          <span class="text-line-strong">★★★★★</span>
          <span class="absolute inset-y-0 start-0 overflow-hidden whitespace-nowrap text-accent" [style.width.%]="fill()">★★★★★</span>
        </span>
        @if (showValue()) {
          <span class="font-semibold text-ink" [class]="size() === 'sm' ? 'text-xs' : 'text-sm'" aria-hidden="true">{{ value() | number: '1.1-1' : language.locale() }}</span>
        }
        @if (count() !== null) {
          <span class="text-muted" [class]="size() === 'sm' ? 'text-xs' : 'text-sm'" aria-hidden="true">({{ count() | number: '1.0-0' : language.locale() }})</span>
        }
      </span>
    }
  `,
})
export class StarRating {
  readonly value = input.required<number | null>();
  readonly count = input<number | null>(null);
  readonly showValue = input(false);
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  protected readonly language = inject(LanguageService);

  protected readonly fill = computed(() => Math.max(0, Math.min(5, this.value() ?? 0)) * 20);
}
