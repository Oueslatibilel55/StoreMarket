import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LocalizedName } from '../../shared/i18n/localized';
import { StatsPeriod } from '../stats/stats-api';

export type StorePaymentKind = 'Subscription' | 'Commission';
export type StorePaymentStatus = 'Declared' | 'Approved' | 'Rejected';
export type StorePaymentMethod = 'Cash' | 'BankTransfer';
export type OfferTarget = 'AllStores' | 'StoreCategories' | 'Stores';
export type InvoiceStatus = 'Issued' | 'PartiallyPaid' | 'Paid';
export type StoreStatus = 'Active' | 'Closed' | 'Disabled' | 'Suspended';

export const PAYMENT_METHODS: StorePaymentMethod[] = ['Cash', 'BankTransfer'];

export interface Offer {
  id: string;
  name: LocalizedName;
  /** Exact number of months; null = any. */
  months: number | null;
  discountPercent: number | null;
  discountAmount: number | null;
  startsOn: string | null;
  endsOn: string | null;
  target: OfferTarget;
  targetIds: string[];
  firstPaymentOnly: boolean;
  isActive: boolean;
  timesUsed: number;
  totalDiscount: number;
}

export interface SaveOffer {
  nameFr: string;
  nameAr: string | null;
  nameEn: string | null;
  months: number | null;
  discountPercent: number | null;
  discountAmount: number | null;
  startsOn: string | null;
  endsOn: string | null;
  target: OfferTarget;
  targetIds: string[];
  firstPaymentOnly: boolean;
  isActive: boolean;
}

export interface QuotedOffer {
  id: string;
  name: LocalizedName;
  discount: number;
  amount: number;
}

export interface SubscriptionQuote {
  months: number;
  monthlyPrice: number;
  gross: number;
  /** Best first. */
  offers: QuotedOffer[];
}

export interface StorePayment {
  id: string;
  number: string;
  storeId: string;
  storeName: string;
  kind: StorePaymentKind;
  status: StorePaymentStatus;
  amount: number;
  method: StorePaymentMethod;
  reference: string | null;
  note: string | null;
  paidOn: string;
  receiptUrl: string | null;
  months: number | null;
  monthlyPrice: number | null;
  offerName: string | null;
  discount: number;
  coversFrom: string | null;
  coversTo: string | null;
  invoiceId: string | null;
  invoiceReference: string | null;
  declaredByOwner: boolean;
  createdAt: string;
  decidedAt: string | null;
  rejectionReason: string | null;
}

export interface InvoiceSummary {
  id: string;
  reference: string;
  /** First day of the invoiced month. */
  month: string;
  dueOn: string;
  total: number;
  paidAmount: number;
  status: InvoiceStatus;
  lineCount: number;
  paymentDeclared: boolean;
  overdue: boolean;
}

export interface InvoiceLine {
  coinEntryId: string;
  type: 'Earned' | 'Adjustment';
  creditedAt: string;
  orderReference: string | null;
  productName: string | null;
  quantity: number | null;
  lineTotal: number | null;
  affiliatePercent: number | null;
  affiliateCode: string | null;
  coins: number;
  coinValueInTnd: number;
  amount: number;
}

export interface InvoiceDetail {
  invoice: InvoiceSummary;
  storeId: string;
  storeName: string;
  storeAddress: string | null;
  storeCity: string;
  issuedAt: string;
  lines: InvoiceLine[];
  payments: StorePayment[];
}

/** The owner's banner / the admin's state of a store. */
export interface BillingStatus {
  late: boolean;
  /** Grace days left before suspension, while something is late. */
  daysLeft: number | null;
  overdue: boolean;
  waitingForApproval: boolean;
  suspended: boolean;
  paidUntil: string;
  commissionsDue: number;
  nextDue: string | null;
  lastRejectionReason: string | null;
}

export interface MonthStatement {
  month: string;
  subscriptionPaid: number;
  subscriptionMonthsPaid: number;
  invoiceId: string | null;
  invoiceReference: string | null;
  commissionsBilled: number;
  commissionsPaid: number;
  invoiceStatus: InvoiceStatus | null;
}

export interface StoreBilling {
  storeId: string;
  storeName: string;
  status: StoreStatus;
  monthlyPrice: number;
  graceDays: number;
  state: BillingStatus;
  /** Commissions credited, not invoiced yet. */
  currentMonth: { month: string; lines: number; amount: number };
  months: MonthStatement[];
  invoices: InvoiceSummary[];
  payments: StorePayment[];
  offers: Offer[];
}

export interface OverdueStore {
  storeId: string;
  storeName: string;
  storeSlug: string;
  status: StoreStatus;
  paidUntil: string;
  commissionsDue: number;
  late: boolean;
  daysLeft: number | null;
  overdue: boolean;
  waitingForApproval: boolean;
}

export interface BillingRevenue {
  period: StatsPeriod;
  months: { month: string; subscriptions: number; commissionsBilled: number; commissionsCollected: number }[];
  offers: Offer[];
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

/** How and when the money was paid (shared by every payment form). */
export interface PaymentFields {
  method: StorePaymentMethod;
  reference?: string | null;
  note?: string | null;
  paidOn?: string | null;
}

export interface DeclarePayment extends PaymentFields {
  kind: StorePaymentKind;
  months?: number | null;
  invoiceId?: string | null;
  amount?: number | null;
  receiptImageId?: string | null;
}

const params = (query: object) => {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(query)) if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
  return p;
};

/** Store billing (025): the admin's side and the store owner's. */
@Injectable({ providedIn: 'root' })
export class BillingApi {
  private readonly http = inject(HttpClient);
  private readonly admin = `${environment.apiUrl}/admin/billing`;
  private readonly mine = `${environment.apiUrl}/me/store/billing`;

  // Admin
  store(id: string): Observable<StoreBilling> {
    return this.http.get<StoreBilling>(`${this.admin}/stores/${id}`);
  }

  setPrice(id: string, monthlyPrice: number, reason: string | null): Observable<StoreBilling> {
    return this.http.put<StoreBilling>(`${this.admin}/stores/${id}/price`, { monthlyPrice, reason });
  }

  quote(id: string, months: number): Observable<SubscriptionQuote> {
    return this.http.get<SubscriptionQuote>(`${this.admin}/stores/${id}/quote`, { params: params({ months }) });
  }

  recordSubscription(id: string, body: PaymentFields & { months: number; offerId: string | null }): Observable<StorePayment> {
    return this.http.post<StorePayment>(`${this.admin}/stores/${id}/subscription-payments`, body);
  }

  invoice(id: string): Observable<InvoiceDetail> {
    return this.http.get<InvoiceDetail>(`${this.admin}/invoices/${id}`);
  }

  recordInvoicePayment(id: string, body: PaymentFields & { amount: number }): Observable<StorePayment> {
    return this.http.post<StorePayment>(`${this.admin}/invoices/${id}/payments`, body);
  }

  payments(query: { status?: StorePaymentStatus; kind?: StorePaymentKind; storeId?: string; page?: number; pageSize?: number }): Observable<Paged<StorePayment>> {
    return this.http.get<Paged<StorePayment>>(`${this.admin}/payments`, { params: params(query) });
  }

  approve(id: string): Observable<StorePayment> {
    return this.http.post<StorePayment>(`${this.admin}/payments/${id}/approve`, {});
  }

  reject(id: string, reason: string): Observable<StorePayment> {
    return this.http.post<StorePayment>(`${this.admin}/payments/${id}/reject`, { reason });
  }

  overdue(): Observable<OverdueStore[]> {
    return this.http.get<OverdueStore[]>(`${this.admin}/overdue`);
  }

  offers(): Observable<Offer[]> {
    return this.http.get<Offer[]>(`${this.admin}/offers`);
  }

  createOffer(body: SaveOffer): Observable<Offer> {
    return this.http.post<Offer>(`${this.admin}/offers`, body);
  }

  updateOffer(id: string, body: SaveOffer): Observable<Offer> {
    return this.http.put<Offer>(`${this.admin}/offers/${id}`, body);
  }

  revenue(query: { from?: string; to?: string }): Observable<BillingRevenue> {
    return this.http.get<BillingRevenue>(`${this.admin}/revenue`, { params: params(query) });
  }

  run(): Observable<{ invoicesIssued: number; storesSuspended: number; storesReactivated: number }> {
    return this.http.post<{ invoicesIssued: number; storesSuspended: number; storesReactivated: number }>(`${this.admin}/run`, {});
  }

  // Store owner
  myBilling(): Observable<StoreBilling> {
    return this.http.get<StoreBilling>(`${this.mine}`);
  }

  myStatus(): Observable<BillingStatus> {
    return this.http.get<BillingStatus>(`${this.mine}/status`);
  }

  myInvoice(id: string): Observable<InvoiceDetail> {
    return this.http.get<InvoiceDetail>(`${this.mine}/invoices/${id}`);
  }

  declare(body: DeclarePayment): Observable<StorePayment> {
    return this.http.post<StorePayment>(`${this.mine}/payments`, body);
  }
}

/** Pill tone of an invoice / payment status (design-system pills). */
export function invoiceTone(status: InvoiceStatus | null, overdue = false): string {
  if (status === 'Paid') return 'pill-success';
  if (overdue) return 'pill-danger';
  return status === 'PartiallyPaid' ? 'pill-warning' : 'pill-neutral';
}

export function paymentTone(status: StorePaymentStatus): string {
  return status === 'Approved' ? 'pill-success' : status === 'Rejected' ? 'pill-danger' : 'pill-warning';
}

/** What is left on an invoice. */
export const remaining = (i: InvoiceSummary) => Math.round((i.total - i.paidAmount) * 1000) / 1000;

/** The banner to show the owner, most urgent first; null = nothing to say. */
export type BannerKind = 'suspended' | 'overdue' | 'countdown' | 'waiting' | 'rejected' | 'due';

export function bannerOf(s: BillingStatus | undefined | null): BannerKind | null {
  if (!s) return null;
  if (s.suspended) return 'suspended';
  if (s.lastRejectionReason) return 'rejected';
  if (s.waitingForApproval) return 'waiting';
  if (s.overdue) return 'overdue';
  if (s.daysLeft !== null) return 'countdown';
  if (s.commissionsDue > 0) return 'due';
  return null;
}
