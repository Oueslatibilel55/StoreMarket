import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, numberAttribute, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { CoinApi, CoinBreakdownBy, CoinBreakdownRow, CoinEntry, CoinEntryType, MAX_EXPORT_ROWS } from '../../../core/coins/coin-api';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { OrderApi, orderStatusTone } from '../../../core/orders/order-api';
import { PartnershipApi } from '../../../core/partnerships/partnership-api';
import { Seo } from '../../../core/seo/seo';
import { Pagination } from '../../../shared/ui/pagination';
import { WalletPayouts } from './wallet-payouts';

const PAGE_SIZE = 20;

export type WalletTab = 'history' | 'breakdown' | 'orders' | 'payouts';

/**
 * `/affiliate/wallet` — the affiliate's wallet (020): the coin history with full filters and a CSV export, the
 * split of earned coins per product or per store over a period, every order line placed with their code, and their
 * payout requests (021).
 * Filters live in the URL so a view can be shared or reloaded.
 */
@Component({
  selector: 'app-wallet',
  imports: [DatePipe, DecimalPipe, RouterLink, TranslocoPipe, Pagination, WalletPayouts],
  templateUrl: './wallet.html',
})
export class Wallet {
  private readonly coinApi = inject(CoinApi);
  private readonly orderApi = inject(OrderApi);
  private readonly partnershipApi = inject(PartnershipApi);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  protected readonly language = inject(LanguageService);

  // Router input binding sets missing query params to undefined, so no defaults here.
  readonly tab = input<WalletTab | undefined>();
  readonly type = input<CoinEntryType | undefined>();
  readonly storeId = input<string | undefined>();
  readonly productId = input<string | undefined>();
  readonly search = input<string | undefined>();
  readonly from = input<string | undefined>();
  readonly to = input<string | undefined>();
  readonly by = input<CoinBreakdownBy | undefined>();
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });

  protected readonly tabs: WalletTab[] = ['history', 'breakdown', 'orders', 'payouts'];
  protected readonly types: CoinEntryType[] = ['Earned', 'Adjustment', 'PayoutLocked', 'PayoutReleased'];
  protected readonly splits: CoinBreakdownBy[] = ['Product', 'Store'];
  protected readonly maxExport = MAX_EXPORT_ROWS;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly statusTone = orderStatusTone;

  protected readonly current = computed<WalletTab>(() => this.tab() ?? 'history');
  protected readonly split = computed<CoinBreakdownBy>(() => this.by() ?? 'Product');
  protected readonly filtered = computed(() =>
    !!(this.type() || this.storeId() || this.productId() || this.search() || this.from() || this.to()));

  protected readonly summary = rxResource({ stream: () => this.coinApi.summary() });
  /** Stores I work or worked with, for the store filter. */
  protected readonly stores = rxResource({ stream: () => this.partnershipApi.listMine() });
  private readonly filters = computed(() => ({
    type: this.type(), storeId: this.storeId(), productId: this.productId(), search: this.search(), from: this.from(), to: this.to(),
  }));
  protected readonly entries = rxResource({
    params: () => (this.current() === 'history' ? { ...this.filters(), page: this.page(), pageSize: PAGE_SIZE } : undefined),
    stream: ({ params }) => this.coinApi.entries(params),
  });
  protected readonly breakdown = rxResource({
    params: () => (this.current() === 'breakdown' ? { by: this.split(), from: this.from(), to: this.to() } : undefined),
    stream: ({ params }) => this.coinApi.breakdown(params.by, params.from, params.to),
  });
  protected readonly breakdownTotal = computed(() => (this.breakdown.value() ?? []).reduce((sum, r) => sum + r.coins, 0));
  protected readonly orders = rxResource({
    params: () => (this.current() === 'orders' ? { page: this.page() } : undefined),
    stream: ({ params }) => this.orderApi.affiliateSales(params.page),
  });

  /** Name of the product the history is filtered on (from the loaded rows). */
  protected readonly productName = computed(() =>
    this.entries.value()?.items.find((e) => e.productId === this.productId())?.productName ?? null);

  protected readonly exporting = signal(false);
  protected readonly exportCapped = signal(false);
  protected readonly error = signal<string | null>(null);
  private searchTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    const seo = inject(Seo);
    effect(() => {
      this.language.lang();
      seo.setKey('wallet.title');
    });
  }

  protected apply(params: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge' });
  }

  protected go(tab: WalletTab): void {
    this.apply({ tab: tab === 'history' ? null : tab, page: null });
  }

  protected clear(): void {
    this.router.navigate([], { queryParams: { tab: this.tab() ?? null } });
  }

  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.apply({ search: value.trim() || null, page: null }), 300);
  }

  /** From a breakdown row to its history: same period, filtered on that product or store. */
  protected history(row: CoinBreakdownRow): void {
    this.apply({
      tab: null, page: null, type: 'Earned',
      productId: this.split() === 'Product' ? row.id : null,
      storeId: this.split() === 'Store' ? row.id : null,
    });
  }

  protected share(row: CoinBreakdownRow): number {
    const total = this.breakdownTotal();
    return total > 0 ? Math.round((row.coins / total) * 100) : 0;
  }

  /** A payout locked or released coins: counters and history change. */
  protected coinsMoved(): void {
    this.summary.reload();
    this.entries.reload();
  }

  /** Downloads the filtered history as a CSV in the current language (";" + BOM so Excel opens it as is). */
  protected download(): void {
    this.error.set(null);
    this.exporting.set(true);
    this.coinApi.export(this.filters()).subscribe({
      next: (rows) => {
        this.exporting.set(false);
        this.exportCapped.set(rows.length >= MAX_EXPORT_ROWS);
        this.save(this.csv(rows), `coins-${new Date().toISOString().slice(0, 10)}.csv`);
      },
      error: (err: unknown) => {
        this.exporting.set(false);
        this.error.set(errorKey(err));
      },
    });
  }

  private csv(rows: CoinEntry[]): string {
    const t = (key: string) => this.transloco.translate(key);
    const headers = ['date', 'type', 'coins', 'reference', 'store', 'product', 'quantity', 'lineTotal', 'percent', 'client', 'phone', 'reason']
      .map((h) => t(`wallet.csv.${h}`));
    const lines = rows.map((e) => [
      e.createdAt, t(`coins.type.${e.type}`), e.coins, e.orderReference, e.storeName, e.productName,
      e.quantity, e.lineTotal, e.affiliatePercent, e.clientName, e.clientPhone, e.reason,
    ].map(cell).join(';'));
    return '﻿' + [headers.map(cell).join(';'), ...lines].join('\r\n');
  }

  private save(content: string, name: string): void {
    const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    a.click();
    URL.revokeObjectURL(url);
  }
}

/** One CSV cell: numbers as is (dot decimals), text quoted, and formula-looking text neutralised. */
function cell(value: string | number | null): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'number') return String(value);
  const text = /^[=+\-@\t\r]/.test(value) ? `'${value}` : value;
  return `"${text.replace(/"/g, '""')}"`;
}
