import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LocalizedName } from '../../shared/i18n/localized';
import { NotDeliveredReason, PaymentStatus, StoreOrderEvent, StoreOrderStatus } from './order-api';

export interface StoreOrderListItem {
  id: string;
  reference: string;
  createdAt: string;
  clientName: string;
  city: string;
  itemCount: number;
  amountToCollect: number;
  status: StoreOrderStatus;
  paymentStatus: PaymentStatus;
  hasAffiliate: boolean;
  placedByAffiliate: boolean;
}

export interface StoreOrderLine {
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
  /** TND the store owes the affiliate if delivered. */
  commission: number | null;
}

export interface StoreOrderDetail {
  id: string;
  reference: string;
  createdAt: string;
  status: StoreOrderStatus;
  paymentStatus: PaymentStatus;
  clientName: string;
  clientPhone: string;
  city: string;
  address: string;
  note: string | null;
  placedByAffiliate: boolean;
  deliveryFee: number;
  subtotal: number;
  amountToCollect: number;
  lines: StoreOrderLine[];
  history: StoreOrderEvent[];
  /** Statuses the store may move it to now. */
  next: StoreOrderStatus[];
}

export interface StoreOrderQuery {
  status?: StoreOrderStatus;
  withAffiliate?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface ChangeStatus {
  status: StoreOrderStatus;
  reason?: string | null;
  notDeliveredReason?: NotDeliveredReason | null;
}

/** The signed-in store owner's orders (016). */
@Injectable({ providedIn: 'root' })
export class StoreOrderApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/me/store/orders`;

  list(query: StoreOrderQuery): Observable<{ items: StoreOrderListItem[]; page: number; pageSize: number; total: number }> {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(query)) if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    return this.http.get<{ items: StoreOrderListItem[]; page: number; pageSize: number; total: number }>(this.base, { params });
  }

  counts(): Observable<{ byStatus: Partial<Record<StoreOrderStatus, number>> }> {
    return this.http.get<{ byStatus: Partial<Record<StoreOrderStatus, number>> }>(`${this.base}/counts`);
  }

  get(id: string): Observable<StoreOrderDetail> {
    return this.http.get<StoreOrderDetail>(`${this.base}/${id}`);
  }

  change(id: string, change: ChangeStatus): Observable<StoreOrderDetail> {
    return this.http.post<StoreOrderDetail>(`${this.base}/${id}/status`, change);
  }
}
