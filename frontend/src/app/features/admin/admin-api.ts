import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Role } from '../../core/auth/auth.models';
import { NotDeliveredReason, Order, StoreOrderStatus } from '../../core/orders/order-api';
import { CoinEntryQuery, CoinSummary, PagedCoinEntries } from '../../core/coins/coin-api';
import { environment } from '../../../environments/environment';
import {
  AdminOrderListItem, AdminOrderQuery, AdminPartnership, AdminRoleRequest, AdminSetting, PartnershipQuery, AdminStoreCategory, AdminUser, AdminUserListItem, AuditEntry, AuditQuery, PagedResult,
  RoleRequestQuery, SaveStoreCategory, UserCounts,
  UserListQuery,
} from './admin.models';

/** Drops empty values so the URL only carries the filters actually set. */
export function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
  }
  return params;
}

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin`;

  listUsers(query: UserListQuery): Observable<PagedResult<AdminUserListItem>> {
    return this.http.get<PagedResult<AdminUserListItem>>(`${this.base}/users`, { params: toParams(query) });
  }

  userCounts(): Observable<UserCounts> {
    return this.http.get<UserCounts>(`${this.base}/users/counts`);
  }

  getUser(id: string): Observable<AdminUser> {
    return this.http.get<AdminUser>(`${this.base}/users/${id}`);
  }

  grantRole(id: string, role: Role): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.base}/users/${id}/roles/${role}`, null);
  }

  revokeRole(id: string, role: Role): Observable<AdminUser> {
    return this.http.delete<AdminUser>(`${this.base}/users/${id}/roles/${role}`);
  }

  block(id: string, reason: string): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.base}/users/${id}/block`, { reason });
  }

  unblock(id: string): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.base}/users/${id}/unblock`, null);
  }

  disableAffiliateCode(id: string, reason: string): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.base}/users/${id}/affiliate-code/disable`, { reason });
  }

  enableAffiliateCode(id: string): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.base}/users/${id}/affiliate-code/enable`, null);
  }

  orders(query: AdminOrderQuery): Observable<PagedResult<AdminOrderListItem>> {
    return this.http.get<PagedResult<AdminOrderListItem>>(`${this.base}/orders`, { params: toParams(query) });
  }

  order(reference: string): Observable<Order> {
    return this.http.get<Order>(`${this.base}/orders/${encodeURIComponent(reference)}`);
  }

  forceStatus(storeOrderId: string, status: StoreOrderStatus, reason: string, notDeliveredReason: NotDeliveredReason | null): Observable<Order> {
    return this.http.post<Order>(`${this.base}/store-orders/${storeOrderId}/status`, { status, reason, notDeliveredReason });
  }

  coinEntries(query: CoinEntryQuery): Observable<PagedCoinEntries> {
    return this.http.get<PagedCoinEntries>(`${this.base}/coins/entries`, { params: toParams(query) });
  }

  coinTotals(): Observable<{ balance: number; totalEarned: number; expected: number; affiliates: number }> {
    return this.http.get<{ balance: number; totalEarned: number; expected: number; affiliates: number }>(`${this.base}/coins/totals`);
  }

  adjustCoins(affiliateId: string, coins: number, reason: string, relatedEntryId: string | null): Observable<CoinSummary> {
    return this.http.post<CoinSummary>(`${this.base}/coins/adjustments`, { affiliateId, coins, reason, relatedEntryId });
  }

  partnerships(query: PartnershipQuery): Observable<PagedResult<AdminPartnership>> {
    return this.http.get<PagedResult<AdminPartnership>>(`${this.base}/partnerships`, { params: toParams(query) });
  }

  endPartnership(id: string, reason: string): Observable<AdminPartnership> {
    return this.http.post<AdminPartnership>(`${this.base}/partnerships/${id}/end`, { reason });
  }

  audit(query: AuditQuery): Observable<PagedResult<AuditEntry>> {
    return this.http.get<PagedResult<AuditEntry>>(`${this.base}/audit`, { params: toParams(query) });
  }

  roleRequests(query: RoleRequestQuery): Observable<PagedResult<AdminRoleRequest>> {
    return this.http.get<PagedResult<AdminRoleRequest>>(`${this.base}/role-requests`, { params: toParams(query) });
  }

  approveRoleRequest(id: string): Observable<unknown> {
    return this.http.post(`${this.base}/role-requests/${id}/approve`, null);
  }

  refuseRoleRequest(id: string, reason: string): Observable<unknown> {
    return this.http.post(`${this.base}/role-requests/${id}/refuse`, { reason });
  }

  settings(): Observable<AdminSetting[]> {
    return this.http.get<AdminSetting[]>(`${this.base}/settings`);
  }

  updateSetting(key: string, value: string, reason: string | null): Observable<AdminSetting> {
    return this.http.put<AdminSetting>(`${this.base}/settings/${key}`, { value, reason });
  }

  storeCategories(): Observable<AdminStoreCategory[]> {
    return this.http.get<AdminStoreCategory[]>(`${this.base}/store-categories`);
  }

  createStoreCategory(body: SaveStoreCategory): Observable<AdminStoreCategory> {
    return this.http.post<AdminStoreCategory>(`${this.base}/store-categories`, body);
  }

  updateStoreCategory(id: string, body: SaveStoreCategory): Observable<AdminStoreCategory> {
    return this.http.put<AdminStoreCategory>(`${this.base}/store-categories/${id}`, body);
  }

  setStoreCategoryActive(id: string, active: boolean): Observable<AdminStoreCategory> {
    return this.http.post<AdminStoreCategory>(`${this.base}/store-categories/${id}/${active ? 'activate' : 'deactivate'}`, null);
  }

  moveStoreCategory(id: string, direction: 'up' | 'down'): Observable<AdminStoreCategory> {
    return this.http.post<AdminStoreCategory>(`${this.base}/store-categories/${id}/move`, { direction });
  }

  deleteStoreCategory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/store-categories/${id}`);
  }
}
