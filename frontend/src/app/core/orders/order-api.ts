import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LocalizedName } from '../../shared/i18n/localized';
import { GuestLine } from '../cart/cart-api';

/** Status of one store's part of an order (016). Labels: `orders.status.<status>`. */
export type StoreOrderStatus = 'Placed' | 'Confirmed' | 'Shipped' | 'Delivered' | 'Cancelled' | 'NotDelivered';
export type NotDeliveredReason = 'Refused' | 'Unreachable' | 'WrongAddress' | 'Other';
export type OrderActorRole = 'Store' | 'Client' | 'Admin';
export type PaymentStatus = 'Unpaid' | 'Collected';

/** Pill tone of a store order status (design system). */
export function orderStatusTone(status: StoreOrderStatus): string {
  switch (status) {
    case 'Delivered': return 'pill-success';
    case 'Cancelled': case 'NotDelivered': return 'pill-danger';
    case 'Shipped': return 'pill-brand';
    case 'Confirmed': return 'pill-neutral';
    default: return 'pill-warning';
  }
}

export interface StoreOrderEvent {
  from: StoreOrderStatus;
  to: StoreOrderStatus;
  actorRole: OrderActorRole;
  reason: string | null;
  notDeliveredReason: NotDeliveredReason | null;
  at: string;
}

export interface OrderContact {
  fullName: string;
  phone: string;
  /** Governorate code. */
  city: string;
  address: string;
  note: string | null;
}

export interface OrderLine {
  id: string;
  productId: string;
  productName: string;
  productSlug: string;
  imageUrl: string | null;
  options: { axis: LocalizedName; value: LocalizedName }[];
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  affiliateCode: string | null;
  affiliatePercent: number | null;
}

export interface StoreOrder {
  id: string;
  storeId: string;
  storeName: string;
  storeSlug: string | null;
  status: StoreOrderStatus;
  paymentStatus: PaymentStatus;
  deliveryFee: number;
  subtotal: number;
  amountToCollect: number;
  lines: OrderLine[];
  history: StoreOrderEvent[];
  /** The client may still cancel (before shipping). */
  canCancel: boolean;
}

export interface Order {
  id: string;
  /** "CMD-2026-000123" */
  reference: string;
  createdAt: string;
  clientName: string;
  clientPhone: string;
  city: string;
  address: string;
  note: string | null;
  placedByAffiliate: boolean;
  itemsTotal: number;
  deliveryTotal: number;
  total: number;
  storeOrders: StoreOrder[];
}

export interface OrderListItem {
  id: string;
  reference: string;
  createdAt: string;
  clientName: string;
  placedByAffiliate: boolean;
  itemCount: number;
  storeNames: string[];
  statuses: StoreOrderStatus[];
  total: number;
}

export interface AffiliateOrderLine {
  lineId: string;
  reference: string;
  createdAt: string;
  storeName: string;
  productName: string;
  /** Who ordered (name only; the phone stays with the store). */
  clientName: string;
  quantity: number;
  lineTotal: number;
  affiliatePercent: number;
  expectedCoins: number;
  status: StoreOrderStatus;
  placedByMe: boolean;
}

export interface AffiliateOrders {
  /** expectedCoins: parcels on their way; earnedCoins: delivered (in the ledger). */
  summary: { lines: number; sales: number; expectedCoins: number; earnedCoins: number };
  lines: { items: AffiliateOrderLine[]; page: number; pageSize: number; total: number };
}

export interface PagedOrders {
  items: OrderListItem[];
  page: number;
  pageSize: number;
  total: number;
}

@Injectable({ providedIn: 'root' })
export class OrderApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  /** Signed in: orders the server cart. Guest: `lines` is the browser cart. `forClient`: affiliate mode. */
  place(contact: OrderContact, forClient: boolean, lines: GuestLine[] | null): Observable<Order> {
    return this.http.post<Order>(`${this.base}/orders`, { contact, forClient, lines });
  }

  mine(page = 1): Observable<PagedOrders> {
    return this.http.get<PagedOrders>(`${this.base}/me/orders`, { params: new HttpParams().set('page', page) });
  }

  get(reference: string): Observable<Order> {
    return this.http.get<Order>(`${this.base}/me/orders/${encodeURIComponent(reference)}`);
  }

  lookup(reference: string, phone: string): Observable<Order> {
    return this.http.get<Order>(`${this.base}/orders/lookup`, { params: new HttpParams().set('reference', reference).set('phone', phone) });
  }

  cancel(reference: string, storeOrderId: string, reason: string | null): Observable<Order> {
    return this.http.post<Order>(`${this.base}/me/orders/${encodeURIComponent(reference)}/store-orders/${storeOrderId}/cancel`, { reason });
  }

  affiliateSales(page = 1): Observable<AffiliateOrders> {
    return this.http.get<AffiliateOrders>(`${this.base}/me/affiliate/orders`, { params: new HttpParams().set('page', page) });
  }
}
