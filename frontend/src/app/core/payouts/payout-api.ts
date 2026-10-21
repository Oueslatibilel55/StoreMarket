import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { toParams } from '../coins/coin-api';

/** Where a payout request stands (021). Labels: `payouts.status.<status>`. */
export type PayoutStatus = 'Requested' | 'Verified' | 'Scheduled' | 'Paid' | 'Rejected' | 'Cancelled';
/** Labels: `payouts.method.<method>`. */
export type PayoutMethod = 'Cash' | 'BankTransfer';

export const PAYOUT_STATUSES: PayoutStatus[] = ['Requested', 'Verified', 'Scheduled', 'Paid', 'Rejected', 'Cancelled'];
/** A Tunisian RIB: 20 digits (spaces allowed while typing). */
export const RIB_LENGTH = 20;

export interface PayoutEvent {
  status: PayoutStatus;
  at: string;
  /** The affiliate's own steps (request, cancel). */
  byAffiliate: boolean;
  /** Who did it — admin view only. */
  actorName: string | null;
  note: string | null;
}

export interface Payout {
  id: string;
  reference: string;
  createdAt: string;
  affiliateId: string;
  affiliateName: string;
  affiliateCode: string | null;
  coins: number;
  coinValueInTnd: number;
  /** Frozen at request time. */
  amountTnd: number;
  method: PayoutMethod;
  bankName: string | null;
  accountHolder: string | null;
  rib: string | null;
  note: string | null;
  status: PayoutStatus;
  /** Cash: handover date + time; transfer: planned date. */
  scheduledFor: string | null;
  place: string | null;
  paidAt: string | null;
  paidReference: string | null;
  rejectionReason: string | null;
  history: PayoutEvent[];
}

export interface PayoutBankDetails {
  bankName: string;
  accountHolder: string;
  rib: string;
}

export interface MyPayouts {
  /** Coins I can ask for now (balance). */
  available: number;
  minimum: number;
  coinValueInTnd: number;
  lastBankDetails: PayoutBankDetails | null;
  items: Payout[];
}

export interface CreatePayout {
  coins: number;
  method: PayoutMethod;
  bankName?: string | null;
  accountHolder?: string | null;
  rib?: string | null;
  note?: string | null;
}

export interface PayoutListItem {
  id: string;
  reference: string;
  createdAt: string;
  affiliateId: string;
  affiliateName: string;
  affiliateCode: string | null;
  coins: number;
  amountTnd: number;
  method: PayoutMethod;
  status: PayoutStatus;
  scheduledFor: string | null;
  place: string | null;
  paidAt: string | null;
}

export interface PagedPayouts {
  items: PayoutListItem[];
  page: number;
  pageSize: number;
  total: number;
}

export interface PayoutQuery {
  status?: PayoutStatus;
  method?: PayoutMethod;
  /** Scheduled between these days (inclusive). */
  from?: string;
  to?: string;
  search?: string;
  affiliateId?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminPayoutCounts {
  requested: number;
  verified: number;
  scheduled: number;
  lockedCoins: number;
}

export interface PayoutCoveredEntry {
  entryId: string;
  creditedAt: string;
  orderReference: string | null;
  storeId: string | null;
  storeName: string | null;
  productName: string | null;
  coins: number;
  /** The part of the entry this request takes. */
  coveredCoins: number;
  /** Set once the store was billed for this commission (025). */
  invoiceId: string | null;
}

export interface AdminPayoutDetail {
  payout: Payout;
  affiliate: {
    id: string;
    fullName: string;
    email: string;
    phone: string | null;
    code: string | null;
    isActive: boolean;
    canEarn: boolean;
    balance: number;
    totalEarned: number;
  };
  covered: PayoutCoveredEntry[];
  adjustmentsTotal: number;
  otherPayouts: PayoutListItem[];
}

export function payoutTone(status: PayoutStatus): string {
  switch (status) {
    case 'Paid': return 'pill-success';
    case 'Rejected': return 'pill-danger';
    case 'Cancelled': return 'pill-neutral';
    case 'Scheduled': return 'pill-brand';
    default: return 'pill-warning';
  }
}

export function isOpenPayout(status: PayoutStatus): boolean {
  return status === 'Requested' || status === 'Verified' || status === 'Scheduled';
}

/** "08123456789012345678" → "08 123 4567890123456 78" (bank · branch · account · key) for reading. */
export function formatRib(rib: string | null): string {
  if (!rib || rib.length !== RIB_LENGTH) return rib ?? '';
  return `${rib.slice(0, 2)} ${rib.slice(2, 5)} ${rib.slice(5, 18)} ${rib.slice(18)}`;
}

/** The signed-in affiliate's payout requests (021). */
@Injectable({ providedIn: 'root' })
export class PayoutApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/me/payouts`;

  mine(): Observable<MyPayouts> {
    return this.http.get<MyPayouts>(this.base);
  }

  request(body: CreatePayout): Observable<Payout> {
    return this.http.post<Payout>(this.base, body);
  }

  cancel(id: string): Observable<Payout> {
    return this.http.post<Payout>(`${this.base}/${id}/cancel`, {});
  }
}

/** Admin side of payouts (021): list, verification screen and the four steps (audited). */
@Injectable({ providedIn: 'root' })
export class AdminPayoutApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/payouts`;

  list(query: PayoutQuery): Observable<PagedPayouts> {
    return this.http.get<PagedPayouts>(this.base, { params: toParams(query) });
  }

  counts(): Observable<AdminPayoutCounts> {
    return this.http.get<AdminPayoutCounts>(`${this.base}/counts`);
  }

  detail(id: string): Observable<AdminPayoutDetail> {
    return this.http.get<AdminPayoutDetail>(`${this.base}/${id}`);
  }

  verify(id: string, note: string | null): Observable<Payout> {
    return this.http.post<Payout>(`${this.base}/${id}/verify`, { note });
  }

  reject(id: string, reason: string): Observable<Payout> {
    return this.http.post<Payout>(`${this.base}/${id}/reject`, { reason });
  }

  schedule(id: string, scheduledFor: string, place: string | null): Observable<Payout> {
    return this.http.post<Payout>(`${this.base}/${id}/schedule`, { scheduledFor, place });
  }

  markPaid(id: string, reference: string, paidAt: string | null): Observable<Payout> {
    return this.http.post<Payout>(`${this.base}/${id}/paid`, { reference, paidAt });
  }
}
