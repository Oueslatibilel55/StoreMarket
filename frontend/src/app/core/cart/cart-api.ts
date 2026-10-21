import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LocalizedName } from '../../shared/i18n/localized';

/** Why a line can't be ordered right now — translated as `cart.issues.<issue>`. */
export type CartIssue = 'productUnavailable' | 'storeClosed' | 'variantRequired' | 'variantUnavailable' | 'outOfStock' | 'notEnoughStock';

export interface CartCodeStatus {
  valid: boolean;
  /** "Sana T." when valid. */
  affiliateName: string | null;
  affiliatePercent: number | null;
  /** `affiliateCode.*` error code when refused — translate as `errors.<code>`. */
  error: string | null;
}

export interface CartLine {
  /** Null for a guest cart (kept in the browser). */
  id: string | null;
  productId: string;
  variantId: string | null;
  productName: string;
  productSlug: string;
  imageUrl: string | null;
  options: { axis: LocalizedName; value: LocalizedName }[];
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  /** Units in stock right now. */
  available: number;
  issue: CartIssue | null;
  affiliateCode: string | null;
  code: CartCodeStatus | null;
}

export interface CartStoreGroup {
  storeId: string;
  name: string;
  slug: string;
  logoUrl: string | null;
  isOpen: boolean;
  deliveryFee: number;
  /** Governorate codes the store delivers to (checked at checkout). */
  deliveryCities: string[];
  /** Orderable lines only, without delivery. */
  subtotal: number;
  lines: CartLine[];
}

export interface Cart {
  stores: CartStoreGroup[];
  itemCount: number;
  itemsTotal: number;
  deliveryTotal: number;
  total: number;
  canCheckout: boolean;
}

/** A line of the guest cart, as stored in the browser. */
export interface GuestLine {
  productId: string;
  variantId: string | null;
  quantity: number;
  affiliateCode: string | null;
}

export const EMPTY_CART: Cart = { stores: [], itemCount: 0, itemsTotal: 0, deliveryTotal: 0, total: 0, canCheckout: false };

@Injectable({ providedIn: 'root' })
export class CartApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/me/cart`;

  get(): Observable<Cart> {
    return this.http.get<Cart>(this.base);
  }

  add(line: GuestLine): Observable<Cart> {
    return this.http.post<Cart>(`${this.base}/lines`, line);
  }

  setQuantity(id: string, quantity: number): Observable<Cart> {
    return this.http.patch<Cart>(`${this.base}/lines/${id}`, { quantity });
  }

  setCode(id: string, code: string | null): Observable<Cart> {
    return code
      ? this.http.put<Cart>(`${this.base}/lines/${id}/code`, { code })
      : this.http.delete<Cart>(`${this.base}/lines/${id}/code`);
  }

  remove(id: string): Observable<Cart> {
    return this.http.delete<Cart>(`${this.base}/lines/${id}`);
  }

  clear(): Observable<Cart> {
    return this.http.delete<Cart>(this.base);
  }

  merge(lines: GuestLine[]): Observable<Cart> {
    return this.http.post<Cart>(`${this.base}/merge`, { lines });
  }

  /** Is this affiliate code valid on this product? (012) */
  checkCode(code: string, productId: string): Observable<{ code: string; affiliateName: string; storeId: string; affiliatePercent: number }> {
    return this.http.post<{ code: string; affiliateName: string; storeId: string; affiliatePercent: number }>(
      `${environment.apiUrl}/affiliate-codes/check`, { code, productId });
  }

  /** A guest's browser cart, shown like a server one (prices, stock, codes checked now). */
  preview(lines: GuestLine[]): Observable<Cart> {
    return this.http.post<Cart>(`${environment.apiUrl}/cart/preview`, { lines });
  }
}
