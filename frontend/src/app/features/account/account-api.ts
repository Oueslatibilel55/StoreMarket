import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Role } from '../../core/auth/auth.models';
import { environment } from '../../../environments/environment';

export type RoleRequestStatus = 'Pending' | 'Approved' | 'Refused' | 'Cancelled';

/** Roles a user can ask for. Admin is never requested. */
export type RequestableRole = Extract<Role, 'Buyer' | 'Affiliate' | 'StoreOwner'>;
export const REQUESTABLE_ROLES: RequestableRole[] = ['Affiliate', 'StoreOwner', 'Buyer'];

export interface RoleRequest {
  id: string;
  role: RequestableRole;
  status: RoleRequestStatus;
  message: string | null;
  decisionReason: string | null;
  decidedAt: string | null;
  createdAt: string;
}

export interface CreateRoleRequest {
  role: RequestableRole;
  message: string | null;
  /** Required for 'Affiliate'. */
  acceptTerms: boolean;
}

@Injectable({ providedIn: 'root' })
export class AccountApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/me/role-requests`;

  myRoleRequests(): Observable<RoleRequest[]> {
    return this.http.get<RoleRequest[]>(this.base);
  }

  requestRole(body: CreateRoleRequest): Observable<RoleRequest> {
    return this.http.post<RoleRequest>(this.base, body);
  }

  cancelRoleRequest(id: string): Observable<RoleRequest> {
    return this.http.post<RoleRequest>(`${this.base}/${id}/cancel`, null);
  }
}
