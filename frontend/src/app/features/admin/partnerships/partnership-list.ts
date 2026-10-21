import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { PartnershipStatus, partnershipTone } from '../../../core/partnerships/partnership-api';
import { Modal } from '../../../shared/ui/modal';
import { Pagination } from '../../../shared/ui/pagination';
import { AdminApi } from '../admin-api';
import { AdminPartnership } from '../admin.models';

const PAGE_SIZE = 20;

/** `/admin/partnerships?status=&search=&page=` — every affiliate ↔ store link; end one for fraud (reason required, audited). */
@Component({
  selector: 'app-partnership-list',
  imports: [DatePipe, RouterLink, TranslocoPipe, Modal, Pagination],
  templateUrl: './partnership-list.html',
})
export class PartnershipList {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly status = input<PartnershipStatus | undefined>();
  readonly search = input<string | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly statuses: PartnershipStatus[] = ['Requested', 'Accepted', 'Refused', 'Ended', 'Cancelled'];
  protected readonly pageSize = PAGE_SIZE;
  protected readonly tone = partnershipTone;
  protected readonly hasFilters = computed(() => !!this.status() || !!this.search());

  protected readonly partnerships = rxResource({
    params: () => ({ status: this.status(), search: this.search(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.partnerships(params),
  });

  protected readonly ending = signal<AdminPartnership | null>(null);
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private searchTimer?: ReturnType<typeof setTimeout>;

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  protected clear(): void {
    this.router.navigate([], { queryParams: {} });
  }

  protected askEnd(partnership: AdminPartnership): void {
    this.error.set(null);
    this.reason.set('');
    this.ending.set(partnership);
  }

  protected close(): void {
    if (!this.busy()) this.ending.set(null);
  }

  protected confirm(): void {
    const partnership = this.ending();
    const reason = this.reason().trim();
    if (!partnership || !reason) return;

    this.busy.set(true);
    this.api.endPartnership(partnership.id, reason).subscribe({
      next: () => {
        this.busy.set(false);
        this.ending.set(null);
        this.partnerships.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
