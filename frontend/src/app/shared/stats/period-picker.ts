import { Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { dayBefore, PERIOD_PRESETS, PeriodQuery, presetOf } from '../../core/stats/stats-api';

const DEFAULT_DAYS = 30;

/**
 * Period of a stats screen (023): last 7 / 30 / 90 / 365 days or two dates. The page keeps `from` / `to` in its URL;
 * the 30-day default is sent as no params at all.
 */
@Component({
  selector: 'app-period-picker',
  imports: [TranslocoPipe],
  template: `
    <div class="flex flex-wrap items-center gap-2">
      <div class="flex rounded-lg border border-line bg-card p-0.5 shadow-card" role="group" [attr.aria-label]="'stats.period' | transloco">
        @for (n of presets; track n) {
          <button type="button" (click)="pick(n)" [attr.aria-pressed]="preset() === n"
            class="cursor-pointer rounded-md px-2.5 py-1 text-sm font-medium transition-colors"
            [class]="preset() === n ? 'bg-brand text-white' : 'text-ink-soft hover:bg-subtle hover:text-ink'">
            {{ 'stats.lastDays' | transloco: { n } }}
          </button>
        }
      </div>
      <div class="flex items-center gap-1.5" dir="ltr">
        <input type="date" [value]="start()" [max]="end()" (change)="custom($any($event.target).value, end())"
          [attr.aria-label]="'stats.from' | transloco" class="field w-auto py-1 text-sm" />
        <span class="text-muted" aria-hidden="true">→</span>
        <input type="date" [value]="end()" [min]="start()" [max]="today" (change)="custom(start(), $any($event.target).value)"
          [attr.aria-label]="'stats.to' | transloco" class="field w-auto py-1 text-sm" />
      </div>
    </div>
  `,
})
export class PeriodPicker {
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();
  readonly changed = output<PeriodQuery>();

  protected readonly presets = PERIOD_PRESETS;
  private readonly now = new Date();
  protected readonly today = dayBefore(this.now, 0);
  protected readonly end = computed(() => this.to() ?? this.today);
  protected readonly start = computed(() => this.from() ?? dayBefore(this.now, DEFAULT_DAYS - 1));
  protected readonly preset = computed(() => presetOf({ from: this.start(), to: this.end() }, this.now));

  protected pick(days: number): void {
    this.changed.emit(days === DEFAULT_DAYS ? {} : { from: dayBefore(this.now, days - 1), to: this.today });
  }

  protected custom(from: string, to: string): void {
    if (from && to) this.changed.emit({ from, to });
  }
}
