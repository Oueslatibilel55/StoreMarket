import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LocalizedName } from '../../shared/i18n/localized';
import { CategoryNode } from '../stores/product-api';

export interface StoreCategory {
  id: string;
  slug: string;
  name: LocalizedName;
  /** Emoji, shown when there is no image. */
  icon: string | null;
  imageUrl: string | null;
}

export type ProductSort = 'Newest' | 'PriceAsc' | 'PriceDesc';

export interface ProductCard {
  id: string;
  slug: string;
  name: string;
  imageUrl: string | null;
  price: number;
  /** Below `price` when a variant is cheaper ("from …"). */
  minPrice: number;
  compareAtPrice: number | null;
  hasVariants: boolean;
  inStock: boolean;
  storeName: string;
  storeSlug: string;
  storeOpen: boolean;
}

export interface StoreCard {
  id: string;
  slug: string;
  name: string;
  logoUrl: string | null;
  bannerUrl: string | null;
  category: { id: string; slug: string; name: LocalizedName };
  city: string;
  isOpen: boolean;
  productCount: number;
  deliveryFee: number;
}

export interface PublicStore {
  id: string;
  name: string;
  slug: string;
  category: { id: string; slug: string; name: LocalizedName };
  description: string | null;
  phone: string;
  city: string;
  address: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  isOpen: boolean;
  defaultAffiliatePercent: number;
  deliveryFee: number;
  deliveryCities: string[];
  deliveryMinDays: number;
  deliveryMaxDays: number;
  facebookUrl: string | null;
  instagramUrl: string | null;
  websiteUrl: string | null;
}

export interface PublicOption {
  key: string;
  name: LocalizedName;
}

export interface PublicAttribute {
  key: string;
  name: LocalizedName;
  type: 'Text' | 'Number' | 'Boolean' | 'SingleChoice' | 'MultiChoice';
  unit: string | null;
  text: string | null;
  number: number | null;
  boolean: boolean | null;
  options: PublicOption[];
}

export interface PublicProduct {
  id: string;
  slug: string;
  name: string;
  description: string | null;
  price: number;
  minPrice: number;
  compareAtPrice: number | null;
  affiliatePercent: number;
  inStock: boolean;
  imageUrls: string[];
  categoryPath: CategoryNode[];
  attributes: PublicAttribute[];
  variantAxes: { key: string; name: LocalizedName; options: PublicOption[] }[];
  variants: { id: string; options: Record<string, string>; price: number; inStock: boolean }[];
  store: { id: string; name: string; slug: string; logoUrl: string | null; isOpen: boolean };
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

const params = (query: object) => {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(query)) if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
  return p;
};

/** Public shop data (no auth). */
@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  storeCategories(): Observable<StoreCategory[]> {
    return this.http.get<StoreCategory[]>(`${this.base}/store-categories`);
  }

  productCategories(): Observable<CategoryNode[]> {
    return this.http.get<CategoryNode[]>(`${this.base}/product-categories`);
  }

  products(query: { category?: string; store?: string; search?: string; sort?: ProductSort; page?: number; pageSize?: number }): Observable<Paged<ProductCard>> {
    return this.http.get<Paged<ProductCard>>(`${this.base}/products`, { params: params(query) });
  }

  product(slug: string): Observable<PublicProduct> {
    return this.http.get<PublicProduct>(`${this.base}/products/${slug}`);
  }

  stores(query: { category?: string; city?: string; search?: string; page?: number; pageSize?: number }): Observable<Paged<StoreCard>> {
    return this.http.get<Paged<StoreCard>>(`${this.base}/stores`, { params: params(query) });
  }

  store(slug: string): Observable<PublicStore> {
    return this.http.get<PublicStore>(`${this.base}/stores/${slug}`);
  }
}
