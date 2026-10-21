import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Requested → Accepted / Refused / Cancelled; Accepted → Ended (012). */
export type PartnershipStatus = 'Requested' | 'Accepted' | 'Refused' | 'Ended' | 'Cancelled';

export interface PartnerStoreRef {
  id: string;
  name: string;
  slug: string;
  logoUrl: string | null;
  isOpen: boolean;
  /** False once an admin disabled or deleted the store. */
  isVisible: boolean;
  defaultAffiliatePercent: number;
}

interface PartnershipBase {
  id: string;
  status: PartnershipStatus;
  message: string | null;
  /** Why the owner refused — optional. */
  decisionReason: string | null;
  endReason: string | null;
  createdAt: string;
  decidedAt: string | null;
  endedAt: string | null;
}

/** A partnership as its affiliate sees it ("My stores"). */
export interface AffiliatePartnership extends PartnershipBase {
  store: PartnerStoreRef;
}

export interface PartnerAffiliate {
  id: string;
  fullName: string;
  code: string | null;
  /** False when the account is blocked, lost the Affiliate role, or the admin deactivated the code. */
  codeActive: boolean;
  memberSince: string;
  activePartnerships: number;
}

/** A partnership as the store owner sees it ("Affiliates" tab). */
export interface StorePartnership extends PartnershipBase {
  affiliate: PartnerAffiliate;
}

/** Statuses that block a new request to the same store. */
export const OPEN_STATUSES: readonly PartnershipStatus[] = ['Requested', 'Accepted'];

/** Pill tone of a partnership status (design system). */
export function partnershipTone(status: PartnershipStatus): string {
  switch (status) {
    case 'Accepted': return 'pill-success';
    case 'Requested': return 'pill-warning';
    case 'Refused': return 'pill-danger';
    default: return 'pill-neutral';
  }
}

/** Link an affiliate shares: the cart (013) reads `ref` and fills the code. */
export function shareLink(path: string, code: string): string {
  return `${location.origin}${path}?ref=${encodeURIComponent(code)}`;
}

@Injectable({ providedIn: 'root' })
export class PartnershipApi {
  private readonly http = inject(HttpClient);
  private readonly mine = `${environment.apiUrl}/me/partnerships`;
  private readonly store = `${environment.apiUrl}/me/store/partnerships`;

  // -- Affiliate
  listMine(): Observable<AffiliatePartnership[]> {
    return this.http.get<AffiliatePartnership[]>(this.mine);
  }

  request(storeId: string, message: string | null): Observable<AffiliatePartnership> {
    return this.http.post<AffiliatePartnership>(this.mine, { storeId, message });
  }

  cancel(id: string): Observable<AffiliatePartnership> {
    return this.http.post<AffiliatePartnership>(`${this.mine}/${id}/cancel`, null);
  }

  // -- Store owner
  listForMyStore(): Observable<StorePartnership[]> {
    return this.http.get<StorePartnership[]>(this.store);
  }

  accept(id: string): Observable<StorePartnership> {
    return this.http.post<StorePartnership>(`${this.store}/${id}/accept`, null);
  }

  refuse(id: string, reason: string | null): Observable<StorePartnership> {
    return this.http.post<StorePartnership>(`${this.store}/${id}/refuse`, { reason });
  }

  end(id: string, reason: string | null): Observable<StorePartnership> {
    return this.http.post<StorePartnership>(`${this.store}/${id}/end`, { reason });
  }
}
