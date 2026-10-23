import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Rating } from '../catalog/catalog-api';

/** Inclusive days, `yyyy-MM-dd` (UTC). The server fills missing ends: the last 30 days. */
export interface StatsPeriod {
  from: string;
  to: string;
}

/** Store orders placed in the period, by their current status. */
export interface ConversionStats {
  placed: number;
  open: number;
  delivered: number;
  cancelled: number;
  notDelivered: number;
}

/** Delivered in the period: items sold (TND, no delivery fee), and the part with an affiliate code. */
export interface SalesStats {
  orders: number;
  amount: number;
  quantity: number;
  ordersViaAffiliates: number;
  amountViaAffiliates: number;
}

/** A ranking / breakdown row: `count` = delivered orders (or credited lines for affiliates), `amount` = TND. */
export interface StatsRow {
  id: string;
  name: string;
  /** Store name for a product, affiliate code for an affiliate. */
  detail: string | null;
  count: number;
  quantity: number;
  amount: number;
  coins: number;
}

export interface DailyStats {
  date: string;
  placed: number;
  delivered: number;
  sales: number;
}

export interface PartnershipCounts {
  requested: number;
  accepted: number;
  refused: number;
  ended: number;
}

export interface AdminOverview {
  period: StatsPeriod;
  orders: ConversionStats;
  sales: SalesStats;
  coins: { earned: number; adjusted: number; expected: number; paidOut: number; paidOutTnd: number };
  payouts: { pending: number; pendingCoins: number; pendingTnd: number; paid: number; paidTnd: number };
  stores: { active: number; closed: number; disabled: number; suspended: number };
  /** Store billing (025): collected / billed in the period; due and waiting = now. */
  billing: { subscriptionsCollected: number; commissionsBilled: number; commissionsCollected: number; commissionsDue: number; declaredPayments: number };
  activeAffiliates: number;
  newUsers: number;
  daily: DailyStats[];
  topStores: StatsRow[];
  topProducts: StatsRow[];
  topAffiliates: StatsRow[];
}

export interface AffiliateStats {
  period: StatsPeriod;
  orders: ConversionStats;
  sales: SalesStats;
  coinsEarned: number;
  coinsExpected: number;
  partnerships: PartnershipCounts;
  byStore: StatsRow[];
  byProduct: StatsRow[];
}

export interface StoreStats {
  period: StatsPeriod;
  orders: ConversionStats;
  sales: SalesStats;
  coinsGenerated: number;
  partnerships: PartnershipCounts;
  rating: Rating;
  topProducts: StatsRow[];
  topAffiliates: StatsRow[];
}

export interface ProductStats {
  period: StatsPeriod;
  id: string;
  name: string;
  slug: string;
  storeId: string;
  storeName: string;
  price: number;
  affiliatePercent: number;
  isVisible: boolean;
  rating: Rating;
  orders: ConversionStats;
  sales: SalesStats;
  coinsEarned: number;
  topAffiliates: StatsRow[];
}

/** What the page asked for: either end may be missing (the server's default then applies). */
export interface PeriodQuery {
  from?: string;
  to?: string;
}

const params = (q: PeriodQuery) => {
  let p = new HttpParams();
  if (q.from) p = p.set('from', q.from);
  if (q.to) p = p.set('to', q.to);
  return p;
};

/** Dashboard numbers (023): admin stats and the store owner's own dashboard. */
@Injectable({ providedIn: 'root' })
export class StatsApi {
  private readonly http = inject(HttpClient);
  private readonly admin = `${environment.apiUrl}/admin/stats`;

  overview(q: PeriodQuery): Observable<AdminOverview> {
    return this.http.get<AdminOverview>(`${this.admin}/overview`, { params: params(q) });
  }

  affiliate(id: string, q: PeriodQuery): Observable<AffiliateStats> {
    return this.http.get<AffiliateStats>(`${this.admin}/affiliates/${id}`, { params: params(q) });
  }

  store(id: string, q: PeriodQuery): Observable<StoreStats> {
    return this.http.get<StoreStats>(`${this.admin}/stores/${id}`, { params: params(q) });
  }

  product(id: string, q: PeriodQuery): Observable<ProductStats> {
    return this.http.get<ProductStats>(`${this.admin}/products/${id}`, { params: params(q) });
  }

  myStore(q: PeriodQuery): Observable<StoreStats> {
    return this.http.get<StoreStats>(`${environment.apiUrl}/me/store/stats`, { params: params(q) });
  }
}

/** Preset periods of the picker, in days ending today. */
export const PERIOD_PRESETS = [7, 30, 90, 365];

/** `yyyy-MM-dd` of a UTC day `days` before `today` (0 = today). */
export function dayBefore(today: Date, days: number): string {
  const d = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate() - days));
  return d.toISOString().slice(0, 10);
}

/** The preset matching a period (last N days up to today), or null for a custom one. */
export function presetOf(period: StatsPeriod, today: Date): number | null {
  if (period.to !== dayBefore(today, 0)) return null;
  return PERIOD_PRESETS.find((n) => period.from === dayBefore(today, n - 1)) ?? null;
}

/** Share of `part` in `whole`, 0–100, rounded; 0 when there is nothing. */
export function percent(part: number, whole: number): number {
  return whole > 0 ? Math.round((part / whole) * 100) : 0;
}
