import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { LocalizedName } from '../../../shared/i18n/localized';

export type AttributeType = 'Text' | 'Number' | 'Boolean' | 'SingleChoice' | 'MultiChoice';
export const ATTRIBUTE_TYPES: AttributeType[] = ['Text', 'Number', 'Boolean', 'SingleChoice', 'MultiChoice'];
export const isChoice = (type: AttributeType) => type === 'SingleChoice' || type === 'MultiChoice';

export interface AttributeOption {
  /** null on new options: made from the French name. */
  key: string | null;
  name: LocalizedName;
}

export interface CategoryAttribute {
  id: string;
  key: string;
  name: LocalizedName;
  type: AttributeType;
  unit: string | null;
  required: boolean;
  filterable: boolean;
  sortOrder: number;
  options: AttributeOption[];
}

export interface AdminProductCategory {
  id: string;
  parentId: string | null;
  slug: string;
  name: LocalizedName;
  sortOrder: number;
  isActive: boolean;
  /** Own attributes only; inherited ones belong to the ancestors in the same list. */
  attributes: CategoryAttribute[];
}

export interface SaveProductCategory {
  parentId: string | null;
  nameFr: string;
  nameAr: string | null;
  nameEn: string | null;
  slug: string | null;
}

export interface SaveAttribute {
  key: string | null;
  nameFr: string;
  nameAr: string | null;
  nameEn: string | null;
  /** Ignored on edit: the type never changes. */
  type: AttributeType;
  unit: string | null;
  required: boolean;
  filterable: boolean;
  options: AttributeOption[];
}

/** Admin API of the product category tree (008). */
@Injectable({ providedIn: 'root' })
export class ProductCategoryApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/product-categories`;

  /** Whole tree, depth-first with sisters in order. */
  list(): Observable<AdminProductCategory[]> {
    return this.http.get<AdminProductCategory[]>(this.base);
  }

  create(body: SaveProductCategory): Observable<AdminProductCategory> {
    return this.http.post<AdminProductCategory>(this.base, body);
  }

  update(id: string, body: SaveProductCategory): Observable<AdminProductCategory> {
    return this.http.put<AdminProductCategory>(`${this.base}/${id}`, body);
  }

  setActive(id: string, active: boolean): Observable<AdminProductCategory> {
    return this.http.post<AdminProductCategory>(`${this.base}/${id}/${active ? 'activate' : 'deactivate'}`, null);
  }

  move(id: string, direction: 'up' | 'down'): Observable<AdminProductCategory> {
    return this.http.post<AdminProductCategory>(`${this.base}/${id}/move`, { direction });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  addAttribute(id: string, body: SaveAttribute): Observable<AdminProductCategory> {
    return this.http.post<AdminProductCategory>(`${this.base}/${id}/attributes`, body);
  }

  updateAttribute(id: string, attributeId: string, body: SaveAttribute): Observable<AdminProductCategory> {
    return this.http.put<AdminProductCategory>(`${this.base}/${id}/attributes/${attributeId}`, body);
  }

  moveAttribute(id: string, attributeId: string, direction: 'up' | 'down'): Observable<AdminProductCategory> {
    return this.http.post<AdminProductCategory>(`${this.base}/${id}/attributes/${attributeId}/move`, { direction });
  }

  deleteAttribute(id: string, attributeId: string): Observable<AdminProductCategory> {
    return this.http.delete<AdminProductCategory>(`${this.base}/${id}/attributes/${attributeId}`);
  }
}

/** Depth (1 = top level) and ancestors, computed from the flat list. */
export function depthOf(all: AdminProductCategory[], category: AdminProductCategory): number {
  return pathOf(all, category).length;
}

/** Root first, the category itself last. */
export function pathOf(all: AdminProductCategory[], category: AdminProductCategory): AdminProductCategory[] {
  const path: AdminProductCategory[] = [];
  for (let c: AdminProductCategory | undefined = category; c && !path.includes(c); c = all.find((x) => x.id === c!.parentId)) {
    path.unshift(c);
  }
  return path;
}
