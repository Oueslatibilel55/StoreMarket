import { Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-pagination',
  imports: [TranslocoPipe],
  template: `
    @if (totalPages() > 1) {
      <nav class="flex flex-wrap items-center justify-between gap-3 text-sm" [attr.aria-label]="'common.pagination' | transloco">
        <span class="text-muted">{{ 'common.pageOf' | transloco: { page: page(), total: totalPages() } }}</span>
        <div class="flex items-center gap-1.5">
          <button type="button" (click)="go(page() - 1)" [disabled]="page() <= 1" [attr.aria-label]="'common.previous' | transloco" [title]="'common.previous' | transloco"
            class="flex size-9 cursor-pointer items-center justify-center rounded-full border border-line-strong bg-card text-ink transition-colors hover:border-ink/40 hover:bg-subtle disabled:pointer-events-none disabled:opacity-40">
            <span class="inline-block rtl:rotate-180" aria-hidden="true">←</span>
          </button>
          <span class="flex size-9 items-center justify-center rounded-full bg-ink font-display text-sm font-bold text-white tabular-nums" aria-current="page" dir="ltr">{{ page() }}</span>
          <button type="button" (click)="go(page() + 1)" [disabled]="page() >= totalPages()" [attr.aria-label]="'common.next' | transloco" [title]="'common.next' | transloco"
            class="flex size-9 cursor-pointer items-center justify-center rounded-full border border-line-strong bg-card text-ink transition-colors hover:border-ink/40 hover:bg-subtle disabled:pointer-events-none disabled:opacity-40">
            <span class="inline-block rtl:rotate-180" aria-hidden="true">→</span>
          </button>
        </div>
      </nav>
    }
  `,
})
export class Pagination {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly pageChange = output<number>();

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));

  protected go(page: number): void {
    if (page >= 1 && page <= this.totalPages()) this.pageChange.emit(page);
  }
}
