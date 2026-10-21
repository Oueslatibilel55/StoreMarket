import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { LocalizedPipe, pickName } from '../../../shared/i18n/localized';
import { Modal } from '../../../shared/ui/modal';
import {
  AdminProductCategory, ATTRIBUTE_TYPES, AttributeType, CategoryAttribute, depthOf, isChoice, pathOf, ProductCategoryApi,
} from './product-category-api';

const MAX_DEPTH = 3;

interface OptionRow {
  key: string | null;
  fr: string;
  ar: string;
  en: string;
}

type Pending =
  | { kind: 'category'; category: AdminProductCategory | null; parentId: string | null }
  | { kind: 'attribute'; attribute: CategoryAttribute | null }
  | { kind: 'toggle' | 'delete'; category: AdminProductCategory }
  | { kind: 'deleteAttribute'; attribute: CategoryAttribute };

/** Tree on the start side, the selected category (`?id=`) and its attributes on the other. */
@Component({
  selector: 'app-product-category-admin',
  imports: [ReactiveFormsModule, TranslocoPipe, LocalizedPipe, Modal],
  templateUrl: './product-category-admin.html',
})
export class ProductCategoryAdmin {
  private readonly api = inject(ProductCategoryApi);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  /** Selected category, from the URL. */
  readonly id = input<string | undefined>();

  protected readonly categories = rxResource({ stream: () => this.api.list() });
  protected readonly types = ATTRIBUTE_TYPES;
  protected readonly maxDepth = MAX_DEPTH;
  protected readonly isChoice = isChoice;

  protected readonly selected = computed(() => this.categories.value()?.find((c) => c.id === this.id()) ?? null);
  /** Attributes the selected category inherits, with the ancestor that defines them. */
  protected readonly inherited = computed(() => {
    const all = this.categories.value() ?? [];
    const s = this.selected();
    if (!s) return [];
    return pathOf(all, s).slice(0, -1).flatMap((c) => c.attributes.map((attribute) => ({ attribute, from: c })));
  });

  protected optionNames(a: CategoryAttribute): string {
    return a.options.map((o) => pickName(o.name, this.language.lang())).join(', ');
  }

  protected depth(c: AdminProductCategory): number {
    return depthOf(this.categories.value() ?? [], c);
  }

  /** Hidden by itself or by a hidden ancestor. */
  protected hidden(c: AdminProductCategory): boolean {
    return pathOf(this.categories.value() ?? [], c).some((x) => !x.isActive);
  }

  protected isFirst(c: AdminProductCategory): boolean {
    return this.sisters(c)[0]?.id === c.id;
  }

  protected isLast(c: AdminProductCategory): boolean {
    const s = this.sisters(c);
    return s[s.length - 1]?.id === c.id;
  }

  private sisters(c: AdminProductCategory): AdminProductCategory[] {
    return (this.categories.value() ?? []).filter((x) => x.parentId === c.parentId);
  }

  /** Parents a category may move under: not itself, not its own branch, not too deep. */
  protected parentOptions(category: AdminProductCategory | null): AdminProductCategory[] {
    const all = this.categories.value() ?? [];
    return all.filter((p) => {
      if (category && pathOf(all, p).some((x) => x.id === category.id)) return false;
      return depthOf(all, p) < MAX_DEPTH;
    });
  }

  protected select(c: AdminProductCategory): void {
    this.router.navigate([], { queryParams: { id: c.id } }).then(() => {
      // Below lg the detail sits under the whole tree: scroll to it.
      if (!matchMedia('(min-width: 64rem)').matches) document.getElementById('category-detail')?.scrollIntoView({ behavior: 'smooth' });
    });
  }

  // -- Category form --------------------------------------------------------------------
  protected readonly categoryForm = inject(NonNullableFormBuilder).group({
    parentId: [''],
    nameFr: ['', [Validators.required, Validators.maxLength(60)]],
    nameAr: ['', Validators.maxLength(60)],
    nameEn: ['', Validators.maxLength(60)],
    slug: ['', [Validators.maxLength(60), Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/)]],
  });

  protected openCategory(category: AdminProductCategory | null, parentId: string | null = null): void {
    this.categoryForm.reset({
      parentId: category?.parentId ?? parentId ?? '',
      nameFr: category?.name.fr ?? '',
      nameAr: category?.name.ar ?? '',
      nameEn: category?.name.en ?? '',
      slug: category?.slug ?? '',
    });
    this.open({ kind: 'category', category, parentId });
  }

  protected saveCategory(): void {
    const p = this.pending();
    if (p?.kind !== 'category') return;
    if (this.categoryForm.invalid) {
      this.categoryForm.markAllAsTouched();
      return;
    }
    const v = this.categoryForm.getRawValue();
    const body = {
      parentId: v.parentId || null, nameFr: v.nameFr.trim(), nameAr: v.nameAr.trim() || null,
      nameEn: v.nameEn.trim() || null, slug: v.slug.trim() || null,
    };
    this.run(p.category ? this.api.update(p.category.id, body) : this.api.create(body), (saved) => this.select(saved));
  }

  // -- Attribute form ---------------------------------------------------------------------
  protected readonly attributeForm = inject(NonNullableFormBuilder).group({
    key: ['', [Validators.maxLength(60), Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/)]],
    nameFr: ['', [Validators.required, Validators.maxLength(60)]],
    nameAr: ['', Validators.maxLength(60)],
    nameEn: ['', Validators.maxLength(60)],
    type: ['Text' as AttributeType],
    unit: ['', Validators.maxLength(16)],
    required: [false],
    filterable: [false],
  });
  protected readonly options = signal<OptionRow[]>([]);
  protected readonly attributeType = signal<AttributeType>('Text');

  protected openAttribute(attribute: CategoryAttribute | null): void {
    this.attributeForm.reset({
      key: attribute?.key ?? '', nameFr: attribute?.name.fr ?? '', nameAr: attribute?.name.ar ?? '', nameEn: attribute?.name.en ?? '',
      type: attribute?.type ?? 'Text', unit: attribute?.unit ?? '', required: attribute?.required ?? false,
      filterable: attribute?.filterable ?? false,
    });
    this.attributeType.set(attribute?.type ?? 'Text');
    this.options.set(attribute?.options.map((o) => ({ key: o.key, fr: o.name.fr, ar: o.name.ar ?? '', en: o.name.en ?? '' }))
      ?? [{ key: null, fr: '', ar: '', en: '' }, { key: null, fr: '', ar: '', en: '' }]);
    this.open({ kind: 'attribute', attribute });
  }

  protected setType(type: AttributeType): void {
    this.attributeForm.controls.type.setValue(type);
    this.attributeType.set(type);
  }

  protected editOption(index: number, field: 'fr' | 'ar' | 'en', value: string): void {
    this.options.update((rows) => rows.map((r, i) => (i === index ? { ...r, [field]: value } : r)));
  }

  protected addOption(): void {
    this.options.update((rows) => [...rows, { key: null, fr: '', ar: '', en: '' }]);
  }

  protected removeOption(index: number): void {
    this.options.update((rows) => rows.filter((_, i) => i !== index));
  }

  protected saveAttribute(): void {
    const p = this.pending();
    const category = this.selected();
    if (p?.kind !== 'attribute' || !category) return;
    if (this.attributeForm.invalid) {
      this.attributeForm.markAllAsTouched();
      return;
    }
    const v = this.attributeForm.getRawValue();
    const type = p.attribute?.type ?? v.type;
    const body = {
      key: p.attribute ? null : v.key.trim() || null,
      nameFr: v.nameFr.trim(), nameAr: v.nameAr.trim() || null, nameEn: v.nameEn.trim() || null,
      type,
      unit: type === 'Number' ? v.unit.trim() || null : null,
      required: v.required,
      filterable: v.filterable,
      options: isChoice(type)
        ? this.options().filter((o) => o.fr.trim()).map((o) => ({ key: o.key, name: { fr: o.fr.trim(), ar: o.ar.trim() || null, en: o.en.trim() || null } }))
        : [],
    };
    this.run(p.attribute ? this.api.updateAttribute(category.id, p.attribute.id, body) : this.api.addAttribute(category.id, body));
  }

  protected moveAttribute(attribute: CategoryAttribute, direction: 'up' | 'down'): void {
    const category = this.selected();
    if (category) this.run(this.api.moveAttribute(category.id, attribute.id, direction), undefined, false);
  }

  protected moveCategory(category: AdminProductCategory, direction: 'up' | 'down'): void {
    this.run(this.api.move(category.id, direction), undefined, false);
  }

  // -- Confirmations ----------------------------------------------------------------------
  protected readonly pending = signal<Pending | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected open(action: Pending): void {
    this.error.set(null);
    this.pending.set(action);
  }

  protected close(): void {
    if (!this.busy()) this.pending.set(null);
  }

  protected confirm(): void {
    const p = this.pending();
    const category = this.selected();
    if (p?.kind === 'toggle') this.run(this.api.setActive(p.category.id, !p.category.isActive));
    if (p?.kind === 'delete') {
      this.run(this.api.delete(p.category.id), () => this.router.navigate([], { queryParams: { id: p.category.parentId } }));
    }
    if (p?.kind === 'deleteAttribute' && category) this.run(this.api.deleteAttribute(category.id, p.attribute.id));
  }

  private run<T>(call: Observable<T>, done?: (value: T) => void, modal = true): void {
    if (modal) this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: (value) => {
        this.busy.set(false);
        this.pending.set(null);
        this.categories.reload();
        done?.(value);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
