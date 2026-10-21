import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LocalizedName } from '../../shared/i18n/localized';

export type StoreStatus = 'Active' | 'Closed' | 'Disabled' | 'Suspended';

export interface StoreCategoryRef {
  id: string;
  slug: string;
  name: LocalizedName;
}

export interface SaveStore {
  name: string;
  /** Empty: made from the name. */
  slug: string | null;
  categoryId: string;
  description: string | null;
  phone: string;
  city: string;
  address: string | null;
  logoImageId: string | null;
  bannerImageId: string | null;
  defaultAffiliatePercent: number;
  /** TND */
  deliveryFee: number;
  deliveryCities: string[];
  deliveryMinDays: number;
  deliveryMaxDays: number;
  facebookUrl: string | null;
  instagramUrl: string | null;
  websiteUrl: string | null;
}

export interface MyStore extends Omit<SaveStore, 'slug' | 'categoryId'> {
  id: string;
  slug: string;
  category: StoreCategoryRef;
  logoUrl: string | null;
  bannerUrl: string | null;
  status: StoreStatus;
  /** TND / month, set by the admin per store. */
  subscriptionMonthlyPrice: number;
  createdAt: string;
  /** Why an admin disabled or deleted the store (010) — show it with `<app-moderation-reason>`. */
  moderationReason: string | null;
  isDeleted: boolean;
}

/** Reason stored by the API when the owner lost the StoreOwner role — translated, unlike reasons typed by an admin. */
export const OWNER_ROLE_REVOKED = 'store.ownerRoleRevoked';

/** The signed-in store owner's store. */
@Injectable({ providedIn: 'root' })
export class StoreApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/me/store`;

  mine(): Observable<MyStore> {
    return this.http.get<MyStore>(this.base);
  }

  create(body: SaveStore): Observable<MyStore> {
    return this.http.post<MyStore>(this.base, body);
  }

  update(body: SaveStore): Observable<MyStore> {
    return this.http.put<MyStore>(this.base, body);
  }

  close(): Observable<MyStore> {
    return this.http.post<MyStore>(`${this.base}/close`, null);
  }

  reopen(): Observable<MyStore> {
    return this.http.post<MyStore>(`${this.base}/reopen`, null);
  }
}
