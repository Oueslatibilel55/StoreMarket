import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Kind of coin movement (019). Labels: `coins.type.<type>`. */
export type CoinEntryType = 'Earned' | 'PayoutLocked' | 'PayoutReleased' | 'Adjustment';

export interface CoinSummary {
  /** Sum of the ledger. */
  balance: number;
  totalEarned: number;
  /** Coins of lines with my code still on their way (not in the ledger). */
  expected: number;
  /** TND value of one coin now (platform setting). */
  coinValueInTnd: number;
  /** Coins in my open payout request (021) — already taken off the balance. */
  locked: number;
  /** Coins of my paid payout requests. */
  paidOut: number;
}

export interface CoinEntry {
  id: string;
  createdAt: string;
  affiliateId: string;
  affiliateName: string;
  affiliateCode: string | null;
  type: CoinEntryType;
  /** Signed. */
  coins: number;
  orderReference: string | null;
  storeId: string | null;
  storeName: string | null;
  productId: string | null;
  productName: string | null;
  clientName: string | null;
  /** Masked in the affiliate's own view. */
  clientPhone: string | null;
  quantity: number | null;
  lineTotal: number | null;
  affiliatePercent: number | null;
  coinValueInTnd: number | null;
  reason: string | null;
  relatedEntryId: string | null;
}

export interface CoinEntryQuery {
  affiliateId?: string;
  storeId?: string;
  productId?: string;
  type?: CoinEntryType;
  search?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedCoinEntries {
  items: CoinEntry[];
  page: number;
  pageSize: number;
  total: number;
}

/** How the wallet splits earned coins (020). */
export type CoinBreakdownBy = 'Product' | 'Store';

/** Earned coins of one product (with its store) or one store over a period. */
export interface CoinBreakdownRow {
  id: string;
  name: string;
  storeName: string | null;
  lines: number;
  quantity: number;
  sales: number;
  coins: number;
}

/** Most rows the export returns (CoinLedgerService.MaxExportRows). */
export const MAX_EXPORT_ROWS = 5000;

export function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [k, v] of Object.entries(query)) if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
  return params;
}

/** The signed-in affiliate's coins (019). The admin side lives in AdminApi. */
@Injectable({ providedIn: 'root' })
export class CoinApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/me/coins`;

  summary(): Observable<CoinSummary> {
    return this.http.get<CoinSummary>(this.base);
  }

  entries(query: CoinEntryQuery = {}): Observable<PagedCoinEntries> {
    return this.http.get<PagedCoinEntries>(`${this.base}/entries`, { params: toParams(query) });
  }

  /** Every entry matching the filters (newest first, capped) — the page turns it into a CSV. */
  export(query: Omit<CoinEntryQuery, 'page' | 'pageSize'> = {}): Observable<CoinEntry[]> {
    return this.http.get<CoinEntry[]>(`${this.base}/export`, { params: toParams(query) });
  }

  breakdown(by: CoinBreakdownBy, from?: string, to?: string): Observable<CoinBreakdownRow[]> {
    return this.http.get<CoinBreakdownRow[]>(`${this.base}/breakdown`, { params: toParams({ by, from, to }) });
  }
}
