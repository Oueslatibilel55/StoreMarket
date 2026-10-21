import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { Modal } from '../../../shared/ui/modal';
import { Pagination } from '../../../shared/ui/pagination';
import { RoleBadge } from '../../../shared/ui/role-badge';
import { AdminApi } from '../admin-api';
import { AdminRoleRequest, RoleRequestStatus } from '../admin.models';

const PAGE_SIZE = 20;
/** `?status=all` lists every request; no status in the URL means the work queue (Pending). */
const ALL = 'all';

type Decision = { kind: 'approve' | 'refuse'; request: AdminRoleRequest };

@Component({
  selector: 'app-role-request-list',
  imports: [DatePipe, RouterLink, TranslocoPipe, Modal, Pagination, RoleBadge],
  templateUrl: './role-request-list.html',
})
export class RoleRequestList {
  private readonly api = inject(AdminApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly status = input<RoleRequestStatus | typeof ALL | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly statuses: (RoleRequestStatus | typeof ALL)[] = ['Pending', 'Approved', 'Refused', 'Cancelled', ALL];
  protected readonly selected = computed(() => this.status() ?? 'Pending');
  protected readonly pageSize = PAGE_SIZE;

  protected readonly requests = rxResource({
    params: () => ({
      status: this.selected() === ALL ? undefined : (this.selected() as RoleRequestStatus),
      page: this.page(),
      pageSize: PAGE_SIZE,
    }),
    stream: ({ params }) => this.api.roleRequests(params),
  });

  protected readonly pending = signal<Decision | null>(null);
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected isSelf(request: AdminRoleRequest): boolean {
    return this.auth.user()?.id === request.userId;
  }

  protected filter(status: RoleRequestStatus | typeof ALL): void {
    this.router.navigate([], { queryParams: { status: status === 'Pending' ? null : status, page: null }, queryParamsHandling: 'merge' });
  }

  protected goTo(page: number): void {
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }

  protected ask(kind: Decision['kind'], request: AdminRoleRequest): void {
    this.error.set(null);
    this.reason.set('');
    this.pending.set({ kind, request });
  }

  protected cancel(): void {
    if (!this.busy()) this.pending.set(null);
  }

  protected confirm(): void {
    const decision = this.pending();
    if (!decision) return;
    const reason = this.reason().trim();
    if (decision.kind === 'refuse' && !reason) return;

    const call = decision.kind === 'approve'
      ? this.api.approveRoleRequest(decision.request.id)
      : this.api.refuseRoleRequest(decision.request.id, reason);

    this.busy.set(true);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.pending.set(null);
        this.requests.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
