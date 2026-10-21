import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LocalizedName } from '../../shared/i18n/localized';

export type ProductStatus = 'Draft' | 'Published' | 'Archived';
export const PRODUCT_STATUSES: ProductStatus[] = ['Draft', 'Published', 'Archived'];

export type FieldType = 'Text' | 'Number' | 'Boolean' | 'SingleChoice' | 'MultiChoice';

export interface FieldOption {
  key: string;
  name: LocalizedName;
}

export interface SchemaField {
  attribute: {
    id: string;
    key: string;
    name: LocalizedName;
    type: FieldType;
    unit: string | null;
    required: boolean;
    filterable: boolean;
    options: FieldOption[];
  };
  definedById: string;
  definedBySlug: string;
  definedByName: LocalizedName;
}

export interface CategoryNode {
  id: string;
  parentId: string | null;
  slug: string;
  name: LocalizedName;
  depth: number;
}

export interface CategorySchema {
  category: CategoryNode;
  path: CategoryNode[];
  attributes: SchemaField[];
}

export interface ValueRequest {
  key: string;
  text?: string | null;
  number?: number | null;
  boolean?: boolean | null;
  options?: string[] | null;
}

export interface VariantRequest {
  id: string | null;
  /** Option key per axis: { taille: 'm', couleur: 'noir' } */
  options: Record<string, string>;
  stock: number;
  price: number | null;
  sku: string | null;
}

export interface SaveProduct {
  name: string;
  slug: string | null;
  categoryId: string;
  description: string | null;
  price: number;
  compareAtPrice: number | null;
  sku: string | null;
  stock: number;
  /** null = the store's default % */
  affiliatePercent: number | null;
  status: ProductStatus;
  imageIds: string[];
  values: ValueRequest[];
  variantAxes: string[];
  variants: VariantRequest[];
}

export interface Product {
  id: string;
  slug: string;
  name: string;
  category: { id: string; slug: string; name: LocalizedName };
  description: string | null;
  price: number;
  compareAtPrice: number | null;
  sku: string | null;
  stock: number;
  totalStock: number;
  affiliatePercent: number | null;
  effectiveAffiliatePercent: number;
  status: ProductStatus;
  images: { id: string; url: string }[];
  /** Canonical stored values: numbers invariant, booleans "true"/"false", choices = option keys joined by commas. */
  values: { key: string; value: string }[];
  variantAxes: string[];
  variants: { id: string; options: Record<string, string>; stock: number; price: number | null; sku: string | null }[];
  createdAt: string;
  updatedAt: string;
  /** Disabled by an admin (010): out of the shop whatever its status; only an admin enables it again. */
  isDisabled: boolean;
  moderationReason: string | null;
}

export interface ProductListItem {
  id: string;
  slug: string;
  name: string;
  mainImageUrl: string | null;
  price: number;
  minPrice: number;
  totalStock: number;
  variantCount: number;
  status: ProductStatus;
  categoryName: LocalizedName;
  updatedAt: string;
  isDisabled: boolean;
  moderationReason: string | null;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

/** The store owner's products, plus the public category tree / schema the product form is built from. */
@Injectable({ providedIn: 'root' })
export class ProductApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/me/store/products`;

  list(query: { search?: string; status?: ProductStatus; page?: number; pageSize?: number }): Observable<Paged<ProductListItem>> {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(query)) if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    return this.http.get<Paged<ProductListItem>>(this.base, { params });
  }

  get(id: string): Observable<Product> {
    return this.http.get<Product>(`${this.base}/${id}`);
  }

  create(body: SaveProduct): Observable<Product> {
    return this.http.post<Product>(this.base, body);
  }

  update(id: string, body: SaveProduct): Observable<Product> {
    return this.http.put<Product>(`${this.base}/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  categories(): Observable<CategoryNode[]> {
    return this.http.get<CategoryNode[]>(`${environment.apiUrl}/product-categories`);
  }

  schema(slug: string): Observable<CategorySchema> {
    return this.http.get<CategorySchema>(`${environment.apiUrl}/product-categories/${slug}/schema`);
  }
}

/** Coins an affiliate earns on one item: the commission in TND divided by what one coin is worth. */
export function coinsPerItem(price: number, percent: number, coinValueInTnd: number): number {
  if (!(price > 0) || !(percent >= 0) || !(coinValueInTnd > 0)) return 0;
  return Math.round(((price * percent) / 100 / coinValueInTnd) * 1000) / 1000;
}

/** Every combination of the chosen axes' options (cartesian product), in the fields' order of choices. */
export function combinations(axes: { key: string; options: string[] }[]): Record<string, string>[] {
  return axes.reduce<Record<string, string>[]>(
    (acc, axis) => acc.flatMap((combo) => axis.options.map((o) => ({ ...combo, [axis.key]: o }))),
    [{}],
  ).filter((c) => Object.keys(c).length === axes.length && axes.length > 0);
}
