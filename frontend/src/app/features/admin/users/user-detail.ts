import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { Role } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { Modal } from '../../../shared/ui/modal';
import { RoleBadge } from '../../../shared/ui/role-badge';
import { AdminApi } from '../admin-api';
import { AdminUser } from '../admin.models';
import { AuditEntryItem } from '../audit/audit-entry-item';
import { AffiliateStats } from '../stats/affiliate-stats';

type PendingAction =
  | { kind: 'grant' | 'revoke'; role: Role }
  | { kind: 'block' }
  | { kind: 'unblock' }
  | { kind: 'disableCode' | 'enableCode' };

/**
 * Roles an admin can toggle here. Affiliate is never granted from here: it comes from sign-up or an
 * approved role request (the user must accept the terms). The last base role (Buyer / Affiliate) can't be removed.
 */
const MANAGEABLE: Role[] = ['StoreOwner', 'Admin'];

@Component({
  selector: 'app-user-detail',
  imports: [DatePipe, RouterLink, TranslocoPipe, Modal, RoleBadge, AuditEntryItem, AffiliateStats],
  templateUrl: './user-detail.html',
})
export class UserDetail {
  private readonly api = inject(AdminApi);
  private readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);

  /** Route param :id */
  readonly id = input.required<string>();

  protected readonly user = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.getUser(params) });
  protected readonly history = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.api.audit({ targetType: 'User', targetId: params, pageSize: 50 }),
  });

  protected readonly isSelf = computed(() => this.auth.user()?.id === this.id());
  protected readonly pending = signal<PendingAction | null>(null);
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected manageable(user: AdminUser): { role: Role; has: boolean }[] {
    const rows = MANAGEABLE.map((role) => ({ role, has: user.roles.includes(role) }));
    // Affiliate can be removed by an admin (fraud), but never granted from here.
    if (user.roles.includes('Affiliate')) rows.unshift({ role: 'Affiliate', has: true });
    // Buyer can be given to an affiliate (or removed while another base role remains).
    rows.unshift({ role: 'Buyer', has: user.roles.includes('Buyer') });
    return rows;
  }

  /** Blocking and deactivating a code (012) always carry a reason. */
  protected needsReason(action: PendingAction): boolean {
    return action.kind === 'block' || action.kind === 'disableCode';
  }

  protected ask(action: PendingAction): void {
    this.error.set(null);
    this.reason.set('');
    this.pending.set(action);
  }

  protected cancel(): void {
    if (!this.busy()) this.pending.set(null);
  }

  protected confirm(): void {
    const action = this.pending();
    if (!action) return;
    const id = this.id();

    let call: Observable<AdminUser>;
    switch (action.kind) {
      case 'grant':
        call = this.api.grantRole(id, action.role);
        break;
      case 'revoke':
        call = this.api.revokeRole(id, action.role);
        break;
      case 'block':
        if (!this.reason().trim()) return;
        call = this.api.block(id, this.reason().trim());
        break;
      case 'unblock':
        call = this.api.unblock(id);
        break;
      case 'disableCode':
        if (!this.reason().trim()) return;
        call = this.api.disableAffiliateCode(id, this.reason().trim());
        break;
      case 'enableCode':
        call = this.api.enableAffiliateCode(id);
        break;
    }

    this.busy.set(true);
    call.subscribe({
      next: (updated) => {
        this.user.set(updated);
        this.history.reload();
        this.busy.set(false);
        this.pending.set(null);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
