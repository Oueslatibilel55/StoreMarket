import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { ModerationReason } from '../../../shared/ui/moderation-reason';
import { AdminApi } from '../admin-api';
import { AuditEntryItem } from '../audit/audit-entry-item';
import { AdminStore, ModerationApi } from './moderation-api';
import { ModerationAction, ModerationDialog } from './moderation-dialog';
import { StoreBillingSection } from '../billing/store-billing-section';
import { StoreStatsSection } from '../stats/store-stats-section';
import { StateBadge } from './state-badge';

/** A store seen by the admin: configuration, owner, product counts, moderation actions and their history. */
@Component({
  selector: 'app-store-moderation-detail',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, LocalizedPipe, ModerationReason, AuditEntryItem, ModerationDialog, StateBadge, StoreBillingSection, StoreStatsSection],
  templateUrl: './store-moderation-detail.html',
})
export class StoreModerationDetail {
  private readonly api = inject(ModerationApi);
  private readonly admin = inject(AdminApi);
  private readonly transloco = inject(TranslocoService);
  protected readonly language = inject(LanguageService);

  /** Route param :id */
  readonly id = input.required<string>();

  protected readonly store = rxResource({ params: () => this.id(), stream: ({ params }) => this.api.store(params) });
  protected readonly history = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.admin.audit({ targetType: 'Store', targetId: params, pageSize: 50 }),
  });

  protected readonly pending = signal<ModerationAction | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected ask(action: ModerationAction): void {
    this.error.set(null);
    this.pending.set(action);
  }

  protected title(action: ModerationAction, store: AdminStore): string {
    return this.transloco.translate(`moderation.stores.confirm.${action}`, { name: store.name });
  }

  protected confirm(reason: string): void {
    const action = this.pending();
    if (!action) return;
    const id = this.id();
    const calls: Record<ModerationAction, () => Observable<AdminStore>> = {
      disable: () => this.api.disableStore(id, reason),
      enable: () => this.api.enableStore(id),
      delete: () => this.api.deleteStore(id, reason),
    };

    this.busy.set(true);
    calls[action]().subscribe({
      next: (updated) => {
        this.store.set(updated);
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
