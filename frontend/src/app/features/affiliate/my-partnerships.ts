import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { errorKey } from '../../core/http/problem';
import { LanguageService } from '../../core/i18n/language.service';
import { AffiliatePartnership, PartnershipApi, partnershipTone, shareLink } from '../../core/partnerships/partnership-api';
import { CoinApi } from '../../core/coins/coin-api';
import { OrderApi, orderStatusTone } from '../../core/orders/order-api';
import { Seo } from '../../core/seo/seo';
import { AffiliateCard } from '../account/affiliate-card/affiliate-card';

/** `/affiliate` — the affiliate's code and every store they work with or asked to (012). */
@Component({
  selector: 'app-my-partnerships',
  imports: [DatePipe, DecimalPipe, NgTemplateOutlet, RouterLink, TranslocoPipe, AffiliateCard],
  templateUrl: './my-partnerships.html',
})
export class MyPartnerships {
  private readonly api = inject(PartnershipApi);
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);

  protected readonly partnerships = rxResource({ stream: () => this.api.listMine() });
  /** Products sold with my code (014) — coins are credited on delivery (019). */
  private readonly orders = inject(OrderApi);
  protected readonly sales = rxResource({ stream: () => this.orders.affiliateSales() });
  /** My coins (019): balance from the ledger, expected from parcels on their way, latest entries. */
  private readonly coinApi = inject(CoinApi);
  protected readonly coins = rxResource({ stream: () => this.coinApi.summary() });
  protected readonly coinHistory = rxResource({ stream: () => this.coinApi.entries({ pageSize: 10 }) });
  protected readonly active = computed(() => this.byStatus('Accepted'));
  protected readonly waiting = computed(() => this.byStatus('Requested'));
  protected readonly history = computed(() =>
    (this.partnerships.value() ?? []).filter((p) => !['Accepted', 'Requested'].includes(p.status)));

  protected readonly tone = partnershipTone;
  protected readonly statusTone = orderStatusTone;
  protected readonly copiedId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('partnerships.mine.title');
    });
  }

  protected link(p: AffiliatePartnership): string {
    return shareLink(`/stores/${p.store.slug}`, this.auth.user()?.affiliateCode ?? '');
  }

  protected async copy(p: AffiliatePartnership): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.link(p));
      this.copiedId.set(p.id);
      setTimeout(() => this.copiedId.set(null), 2000);
    } catch {
      // Clipboard blocked: the link is visible in the field to copy by hand.
    }
  }

  protected withdraw(id: string): void {
    this.error.set(null);
    this.api.cancel(id).subscribe({
      next: () => this.partnerships.reload(),
      error: (err: unknown) => this.error.set(errorKey(err)),
    });
  }

  private byStatus(status: AffiliatePartnership['status']): AffiliatePartnership[] {
    return (this.partnerships.value() ?? []).filter((p) => p.status === status);
  }
}
