import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { AccountApi, REQUESTABLE_ROLES, RequestableRole } from '../account-api';

/**
 * Ask the admin for another role (affiliate, store owner, buyer) and follow the requests.
 * Nothing is self-service: the role only arrives once an admin approves.
 */
@Component({
  selector: 'app-role-requests',
  imports: [DatePipe, TranslocoPipe],
  templateUrl: './role-requests.html',
})
export class RoleRequests {
  private readonly api = inject(AccountApi);
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);

  protected readonly requests = rxResource({ stream: () => this.api.myRoleRequests() });

  /** Roles the user doesn't hold and hasn't already asked for. */
  protected readonly available = computed(() => {
    const roles = this.auth.user()?.roles ?? [];
    const pending = (this.requests.value() ?? []).filter((r) => r.status === 'Pending').map((r) => r.role);
    return REQUESTABLE_ROLES.filter((r) => !roles.includes(r) && !pending.includes(r));
  });

  protected readonly role = signal<RequestableRole | null>(null);
  protected readonly message = signal('');
  protected readonly termsAccepted = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Own code never pays on own store / own purchases — worth saying when the two roles meet. */
  protected readonly showSelfAffiliationNote = computed(() => {
    const role = this.role();
    return (role === 'Affiliate' && this.auth.hasRole('StoreOwner')) || (role === 'StoreOwner' && this.auth.hasRole('Affiliate'));
  });

  constructor() {
    // An admin approved a request since the token was issued: refresh once to pick up the new role.
    let refreshed = false;
    effect(() => {
      const roles = this.auth.user()?.roles ?? [];
      const missing = (this.requests.value() ?? []).some((r) => r.status === 'Approved' && !roles.includes(r.role));
      if (missing && !refreshed) {
        refreshed = true;
        this.auth.refresh().subscribe();
      }
    });
  }

  protected choose(role: RequestableRole): void {
    this.role.set(role);
    this.termsAccepted.set(false);
    this.error.set(null);
  }

  protected canSubmit(): boolean {
    const role = this.role();
    return !!role && !this.submitting() && (role !== 'Affiliate' || this.termsAccepted());
  }

  protected submit(): void {
    const role = this.role();
    if (!role || !this.canSubmit()) return;
    this.submitting.set(true);
    this.error.set(null);

    this.api.requestRole({ role, message: this.message().trim() || null, acceptTerms: this.termsAccepted() }).subscribe({
      next: () => {
        this.role.set(null);
        this.message.set('');
        this.submitting.set(false);
        this.requests.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.submitting.set(false);
      },
    });
  }

  protected cancel(id: string): void {
    this.api.cancelRoleRequest(id).subscribe({
      next: () => this.requests.reload(),
      error: (err: unknown) => this.error.set(errorKey(err)),
    });
  }
}
