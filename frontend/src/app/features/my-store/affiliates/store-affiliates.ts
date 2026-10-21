import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { PartnershipApi, partnershipTone, StorePartnership } from '../../../core/partnerships/partnership-api';
import { Modal } from '../../../shared/ui/modal';
import { MyStoreTabs } from '../my-store-tabs';

type Decision = { kind: 'accept' | 'refuse' | 'end'; partnership: StorePartnership };

/**
 * `/my-store/affiliates` — affiliates who asked to work with the store, the partners, and the history (012).
 * Only the store owner decides; a refusal or an end can carry a reason the affiliate sees.
 */
@Component({
  selector: 'app-store-affiliates',
  imports: [DatePipe, NgTemplateOutlet, TranslocoPipe, Modal, MyStoreTabs],
  templateUrl: './store-affiliates.html',
})
export class StoreAffiliates {
  private readonly api = inject(PartnershipApi);
  protected readonly language = inject(LanguageService);

  protected readonly partnerships = rxResource({ stream: () => this.api.listForMyStore() });
  protected readonly requests = computed(() => this.by('Requested'));
  protected readonly partners = computed(() => this.by('Accepted'));
  protected readonly history = computed(() =>
    (this.partnerships.value() ?? []).filter((p) => !['Requested', 'Accepted'].includes(p.status)));

  protected readonly tone = partnershipTone;
  protected readonly pending = signal<Decision | null>(null);
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected ask(kind: Decision['kind'], partnership: StorePartnership): void {
    this.error.set(null);
    this.reason.set('');
    this.pending.set({ kind, partnership });
  }

  protected close(): void {
    if (!this.busy()) this.pending.set(null);
  }

  protected confirm(): void {
    const decision = this.pending();
    if (!decision) return;
    const id = decision.partnership.id;
    const reason = this.reason().trim() || null;

    let call: Observable<StorePartnership>;
    switch (decision.kind) {
      case 'accept':
        call = this.api.accept(id);
        break;
      case 'refuse':
        call = this.api.refuse(id, reason);
        break;
      case 'end':
        call = this.api.end(id, reason);
        break;
    }

    this.busy.set(true);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.pending.set(null);
        this.partnerships.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }

  private by(status: StorePartnership['status']): StorePartnership[] {
    return (this.partnerships.value() ?? []).filter((p) => p.status === status);
  }
}
