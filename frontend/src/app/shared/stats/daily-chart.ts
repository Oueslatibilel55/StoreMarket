import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { DailyStats } from '../../core/stats/stats-api';

export type DailyMetric = 'sales' | 'placed' | 'delivered';
export const DAILY_METRICS: DailyMetric[] = ['sales', 'placed', 'delivered'];

/** One bar: a day, or a week once the period is too long for one bar per day. */
export interface Bar {
  from: string;
  to: string;
  placed: number;
  delivered: number;
  sales: number;
}

/** Days up to `maxDays` stay one bar each; longer periods add up per 7 days (the last bar may be shorter). */
export function toBars(daily: DailyStats[], maxDays = 92): Bar[] {
  const size = daily.length > maxDays ? 7 : 1;
  const bars: Bar[] = [];
  for (let i = 0; i < daily.length; i += size) {
    const days = daily.slice(i, i + size);
    bars.push({
      from: days[0].date,
      to: days[days.length - 1].date,
      placed: days.reduce((s, d) => s + d.placed, 0),
      delivered: days.reduce((s, d) => s + d.delivered, 0),
      sales: days.reduce((s, d) => s + d.sales, 0),
    });
  }
  return bars;
}

/**
 * Daily bars of the dashboard (023): one measure at a time (sales TND, placed or delivered orders) — never two scales
 * on one chart. Hover / focus shows the bar's value; the same numbers are in a table for screen readers.
 */
@Component({
  selector: 'app-daily-chart',
  imports: [DatePipe, DecimalPipe, TranslocoPipe],
  template: `
    <section class="card">
      <div class="card-header">
        <h2 class="section-title">{{ 'stats.daily.' + metric() | transloco }}</h2>
        <div class="flex max-w-full overflow-x-auto rounded-full border border-line p-0.5 [scrollbar-width:none]" role="group" [attr.aria-label]="'stats.daily.measure' | transloco">
          @for (m of metrics; track m) {
            <button type="button" (click)="metric.set(m)" [attr.aria-pressed]="metric() === m"
              class="shrink-0 cursor-pointer rounded-full px-3 py-1 text-xs font-medium whitespace-nowrap transition-colors"
              [class]="metric() === m ? 'bg-brand-soft font-semibold text-brand' : 'text-muted hover:bg-subtle hover:text-ink'">
              {{ 'stats.daily.short.' + m | transloco }}
            </button>
          }
        </div>
      </div>
      <div class="card-body">
        <!-- Value of the hovered bar, else the period total. -->
        <p class="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-sm text-muted" aria-live="polite">
          @if (hovered(); as b) {
            <span>{{ b.from | date: 'mediumDate' : undefined : language.locale() }}@if (b.to !== b.from) { – {{ b.to | date: 'mediumDate' : undefined : language.locale() }} }</span>
          } @else {
            <span>{{ 'stats.daily.total' | transloco }}</span>
          }
          <strong class="figure text-xl" [class]="metric() === 'sales' ? 'text-success' : 'text-ink'" dir="ltr">{{ value(hovered() ?? total()) | number: metric() === 'sales' ? '1.0-3' : '1.0-0' : language.locale() }}@if (metric() === 'sales') { TND }</strong>
        </p>

        <div class="relative mt-5 h-40" aria-hidden="true">
          <!-- Recessive grid: top (max) and middle, thin hairlines. -->
          <div class="absolute inset-x-0 top-0 border-t border-line"></div>
          <div class="absolute inset-x-0 top-1/2 border-t border-line"></div>
          <span class="absolute -top-2.5 end-0 bg-card ps-1 text-[10px] text-muted tabular-nums" dir="ltr">{{ max() | number: '1.0-0' : language.locale() }}</span>
          <div class="absolute inset-0 flex items-end gap-0.5 border-b border-line-strong" dir="ltr" (mouseleave)="hover.set(null)">
            @for (b of bars(); track b.from; let i = $index) {
              <div class="flex h-full min-w-0 flex-1 cursor-default items-end" (mouseenter)="hover.set(i)">
                <!-- Money (sales TND) in olive, order counts in Sidi Bou Said blue. -->
                <div class="w-full rounded-t-[3px] transition-opacity"
                  [class]="(metric() === 'sales' ? 'bg-success' : 'bg-brand') + (hover() !== null && hover() !== i ? ' opacity-55' : '')"
                  [style.height.%]="height(b)" [style.min-height.px]="value(b) > 0 ? 2 : 0"></div>
              </div>
            }
          </div>
        </div>
        <div class="mt-1.5 flex justify-between text-[11px] text-muted" dir="ltr">
          <span>{{ bars()[0]?.from | date: 'd MMM' : undefined : language.locale() }}</span>
          <span>{{ bars()[bars().length - 1]?.to | date: 'd MMM' : undefined : language.locale() }}</span>
        </div>

        <details class="mt-4 text-sm">
          <summary class="cursor-pointer text-muted hover:text-ink">{{ 'stats.daily.table' | transloco }}</summary>
          <div class="mt-3 max-h-64 overflow-auto rounded-lg border border-line">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">{{ 'stats.daily.date' | transloco }}</th>
                  @for (m of metrics; track m) { <th scope="col" class="text-end">{{ 'stats.daily.short.' + m | transloco }}</th> }
                </tr>
              </thead>
              <tbody>
                @for (b of bars(); track b.from) {
                  <tr>
                    <td class="whitespace-nowrap">{{ b.from | date: 'mediumDate' : undefined : language.locale() }}</td>
                    <td class="text-end tabular-nums" dir="ltr">{{ b.sales | number: '1.0-3' : language.locale() }}</td>
                    <td class="text-end tabular-nums" dir="ltr">{{ b.placed }}</td>
                    <td class="text-end tabular-nums" dir="ltr">{{ b.delivered }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </details>
      </div>
    </section>
  `,
})
export class DailyChart {
  protected readonly language = inject(LanguageService);

  readonly daily = input.required<DailyStats[]>();

  protected readonly metrics = DAILY_METRICS;
  protected readonly metric = signal<DailyMetric>('sales');
  protected readonly hover = signal<number | null>(null);
  protected readonly bars = computed(() => toBars(this.daily()));
  protected readonly hovered = computed(() => {
    const i = this.hover();
    return i === null ? null : (this.bars()[i] ?? null);
  });
  protected readonly total = computed<Bar>(() => toBars(this.daily(), Infinity).reduce(
    (t, b) => ({ ...t, placed: t.placed + b.placed, delivered: t.delivered + b.delivered, sales: t.sales + b.sales }),
    { from: '', to: '', placed: 0, delivered: 0, sales: 0 },
  ));
  protected readonly max = computed(() => Math.max(0, ...this.bars().map((b) => this.value(b))));

  protected value(b: Bar): number {
    return b[this.metric()];
  }

  protected height(b: Bar): number {
    const max = this.max();
    return max > 0 ? (this.value(b) / max) * 100 : 0;
  }
}
