import { Component, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Pagination } from '../../../shared/ui/pagination';
import { AdminApi } from '../admin-api';
import { AuditEntryItem } from './audit-entry-item';

const PAGE_SIZE = 30;

@Component({
  selector: 'app-audit-log',
  imports: [TranslocoPipe, Pagination, AuditEntryItem],
  template: `
    <header class="flex flex-wrap items-end justify-between gap-4">
      <div class="min-w-0">
        <h1 class="page-title">{{ 'admin.audit.title' | transloco }}</h1>
        <p class="page-subtitle">{{ 'admin.audit.subtitle' | transloco }}</p>
      </div>
    </header>

    <div class="card mt-6 overflow-hidden">
      @if (entries.value(); as result) {
        @if (result.items.length) {
          <ul class="divide-y divide-line">
            @for (e of result.items; track e.id) {
              <app-audit-entry-item [entry]="e" />
            }
          </ul>
          <div class="border-t border-line px-5 py-3">
            <app-pagination [page]="result.page" [pageSize]="pageSize" [total]="result.total" (pageChange)="goTo($event)" />
          </div>
        } @else {
          <p class="empty-state">{{ 'admin.audit.empty' | transloco }}</p>
        }
      } @else {
        <p class="empty-state">{{ 'common.loading' | transloco }}</p>
      }
    </div>
  `,
})
export class AuditLog {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);

  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });
  protected readonly pageSize = PAGE_SIZE;

  protected readonly entries = rxResource({
    params: () => ({ page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.audit(params),
  });

  protected goTo(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }
}
