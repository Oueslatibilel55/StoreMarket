import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, numberAttribute } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Role } from '../../../core/auth/auth.models';
import { LanguageService } from '../../../core/i18n/language.service';
import { Pagination } from '../../../shared/ui/pagination';
import { RoleBadge } from '../../../shared/ui/role-badge';
import { AdminApi } from '../admin-api';
import { UserStatus } from '../admin.models';

const PAGE_SIZE = 20;

/** Filters live in the URL (?search=&role=&status=&page=) so lists can be shared and the back button works. */
@Component({
  selector: 'app-user-list',
  imports: [DatePipe, RouterLink, TranslocoPipe, Pagination, RoleBadge],
  templateUrl: './user-list.html',
})
export class UserList {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly search = input<string | undefined>();
  readonly role = input<Role | undefined>();
  readonly status = input<UserStatus | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly roles: Role[] = ['Buyer', 'Affiliate', 'StoreOwner', 'Admin'];
  protected readonly statuses: UserStatus[] = ['Active', 'Blocked'];
  protected readonly pageSize = PAGE_SIZE;

  protected readonly users = rxResource({
    params: () => ({ search: this.search(), role: this.role(), status: this.status(), page: this.page(), pageSize: PAGE_SIZE }),
    stream: ({ params }) => this.api.listUsers(params),
  });
  protected readonly counts = rxResource({ stream: () => this.api.userCounts() });
  protected readonly hasFilters = computed(() => !!(this.search() || this.role() || this.status()));

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected clear(): void {
    this.router.navigate([], { queryParams: {} });
  }
}
