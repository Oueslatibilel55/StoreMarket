import { Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/** A filter set by a link (a dashboard number, 023) that has no control on the list page. */
export interface FilterChip {
  /** i18n key, given `{ value }`. */
  label: string;
  value: string;
  /** Query params that remove it. */
  clear: Record<string, null>;
}

/** Shows those filters above a list, each removable, so the admin knows why the list is short. */
@Component({
  selector: 'app-filter-chips',
  imports: [TranslocoPipe],
  template: `
    @if (chips().length) {
      <div class="flex flex-wrap items-center gap-2 border-b border-line px-4 py-3 sm:px-5">
        @for (c of chips(); track c.label) {
          <span class="inline-flex max-w-full min-w-0 items-center gap-1 rounded-full bg-brand-soft py-1 ps-3 pe-1 text-sm font-medium text-brand ring-1 ring-brand/15 ring-inset">
            <span class="truncate">{{ c.label | transloco: { value: c.value } }}</span>
            <button type="button" (click)="cleared.emit({ ...c.clear, page: null })" [attr.aria-label]="'filters.remove' | transloco"
              class="flex size-5 shrink-0 cursor-pointer items-center justify-center rounded-full text-brand transition-colors hover:bg-brand hover:text-white">
              <span aria-hidden="true">×</span>
            </button>
          </span>
        }
      </div>
    }
  `,
})
export class FilterChips {
  readonly chips = input.required<FilterChip[]>();
  readonly cleared = output<Record<string, null>>();
}

/** Ids are long: the chip shows their start (enough to tell two apart). */
export const shortId = (id: string) => id.slice(0, 8);
