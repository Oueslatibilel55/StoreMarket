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
      <div class="flex flex-wrap items-center gap-2 border-b border-line bg-subtle/40 px-4 py-2.5 text-sm">
        @for (c of chips(); track c.label) {
          <span class="pill pill-brand gap-1.5">
            {{ c.label | transloco: { value: c.value } }}
            <button type="button" (click)="cleared.emit({ ...c.clear, page: null })" class="cursor-pointer" [attr.aria-label]="'filters.remove' | transloco">×</button>
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
