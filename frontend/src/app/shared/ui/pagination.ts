import { Component, computed, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-pagination',
  imports: [TranslocoPipe],
  template: `
    @if (totalPages() > 1) {
      <nav class="flex items-center justify-between gap-3 text-sm" [attr.aria-label]="'common.pagination' | transloco">
        <span class="text-muted">{{ 'common.pageOf' | transloco: { page: page(), total: totalPages() } }}</span>
        <div class="flex gap-2">
          <button type="button" (click)="go(page() - 1)" [disabled]="page() <= 1" class="btn btn-secondary btn-sm">
            <span class="inline-block rtl:rotate-180" aria-hidden="true">←</span>
            {{ 'common.previous' | transloco }}
          </button>
          <button type="button" (click)="go(page() + 1)" [disabled]="page() >= totalPages()" class="btn btn-secondary btn-sm">
            {{ 'common.next' | transloco }}
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
