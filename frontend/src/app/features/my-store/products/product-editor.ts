import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { catchError, of } from 'rxjs';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { PlatformSettings } from '../../../core/settings/platform-settings';
import { StoreApi } from '../../../core/stores/store-api';
import {
  coinsPerItem, combinations, Product, PRODUCT_STATUSES, ProductApi, ProductStatus, SaveProduct, SchemaField, ValueRequest,
} from '../../../core/stores/product-api';
import { LocalizedPipe, pickName } from '../../../shared/i18n/localized';
import { ImageUpload } from '../../../shared/ui/image-upload';
import { Modal } from '../../../shared/ui/modal';
import { MyStoreTabs } from '../my-store-tabs';

const MAX_IMAGES = 8;
const MAX_AXES = 2;

interface FieldValue {
  text: string;
  number: number | null;
  boolean: boolean | null;
  options: string[];
}

interface VariantRow {
  id: string | null;
  stock: number;
  price: number | null;
  sku: string;
}

const comboKey = (options: Record<string, string>) =>
  Object.keys(options).sort().map((k) => `${k}=${options[k]}`).join('|');

/** Create (`/my-store/products/new`) or edit (`/my-store/products/:id`) a product. */
@Component({
  selector: 'app-product-editor',
  imports: [ReactiveFormsModule, RouterLink, TranslocoPipe, DecimalPipe, LocalizedPipe, ImageUpload, Modal, MyStoreTabs],
  templateUrl: './product-editor.html',
})
export class ProductEditor {
  private readonly api = inject(ProductApi);
  private readonly router = inject(Router);
  private readonly settings = inject(PlatformSettings);
  protected readonly language = inject(LanguageService);

  /** Route param :id (undefined on /new). */
  readonly id = input<string | undefined>();

  protected readonly product = rxResource({
    params: () => this.id(),
    stream: ({ params }) => (params ? this.api.get(params) : of(null)),
  });
  protected readonly categories = rxResource({ stream: () => this.api.categories() });
  private readonly store = toSignal(inject(StoreApi).mine().pipe(catchError(() => of(null))), { initialValue: null });

  protected readonly statuses = PRODUCT_STATUSES;
  protected readonly maxImages = MAX_IMAGES;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    slug: ['', [Validators.maxLength(60), Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/)]],
    categoryId: ['', Validators.required],
    description: ['', Validators.maxLength(5000)],
    price: [0, [Validators.required, Validators.min(0.001), Validators.max(1_000_000)]],
    compareAtPrice: [null as number | null, [Validators.min(0.001)]],
    sku: ['', Validators.maxLength(64)],
    stock: [0, [Validators.required, Validators.min(0), Validators.max(1_000_000)]],
    affiliatePercent: [null as number | null, [Validators.min(0), Validators.max(100)]],
    status: ['Draft' as ProductStatus],
  });
  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly images = signal<{ id: string; url: string }[]>([]);
  protected readonly values = signal<Record<string, FieldValue>>({});
  protected readonly axes = signal<string[]>([]);
  /** Options offered per axis (the owner ticks which sizes / colours exist). */
  protected readonly axisOptions = signal<Record<string, string[]>>({});
  /** Stock / price / SKU per combination, kept while the owner ticks options on and off. */
  protected readonly rows = signal<Record<string, VariantRow>>({});

  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly confirmDelete = signal(false);

  // -- Category schema -------------------------------------------------------------------
  private readonly categoryId = computed(() => this.formValue().categoryId ?? '');
  private readonly categorySlug = computed(() => this.categories.value()?.find((c) => c.id === this.categoryId())?.slug
    ?? (this.product.value()?.category.id === this.categoryId() ? this.product.value()?.category.slug : undefined));
  protected readonly schema = rxResource({
    params: () => this.categorySlug(),
    stream: ({ params }) => this.api.schema(params),
  });
  protected readonly fields = computed(() => this.schema.value()?.attributes ?? []);
  protected readonly choiceFields = computed(() => this.fields().filter((f) => this.isChoice(f)));
  /** Fields filled once for the product (variant axes are filled per variant). */
  protected readonly productFields = computed(() => this.fields().filter((f) => !this.axes().includes(f.attribute.key)));

  protected readonly combos = computed(() =>
    combinations(this.axes().map((key) => ({
      key,
      options: (this.fields().find((f) => f.attribute.key === key)?.attribute.options ?? [])
        .map((o) => o.key).filter((o) => (this.axisOptions()[key] ?? []).includes(o)),
    }))));

  // -- Affiliate preview -----------------------------------------------------------------
  protected readonly percentMin = computed(() => this.settings.number('affiliatePercent.min', 0));
  protected readonly percentMax = computed(() => this.settings.number('affiliatePercent.max', 50));
  protected readonly storePercent = computed(() => this.store()?.defaultAffiliatePercent ?? 0);
  protected readonly effectivePercent = computed(() => this.formValue().affiliatePercent ?? this.storePercent());
  protected readonly coins = computed(() =>
    coinsPerItem(this.formValue().price ?? 0, this.effectivePercent(), this.settings.number('coin.valueInTnd', 1)));

  constructor() {
    effect(() => {
      const p = this.product.value();
      if (p) untracked(() => this.fill(p));
    });
  }

  protected fieldOf(key: string): SchemaField | undefined {
    return this.fields().find((f) => f.attribute.key === key);
  }

  /** Number typed in an input, or null when empty. */
  protected num(event: Event): number | null {
    const raw = (event.target as HTMLInputElement).value;
    return raw === '' || !Number.isFinite(Number(raw)) ? null : Number(raw);
  }

  protected isChoice(f: SchemaField): boolean {
    return f.attribute.type === 'SingleChoice' || f.attribute.type === 'MultiChoice';
  }

  protected optionName(f: SchemaField, key: string): string {
    const o = f.attribute.options.find((x) => x.key === key);
    return o ? pickName(o.name, this.language.lang()) : key;
  }

  protected comboLabel(combo: Record<string, string>): string {
    return this.axes().map((k) => {
      const f = this.fields().find((x) => x.attribute.key === k);
      return f ? this.optionName(f, combo[k]) : combo[k];
    }).join(' · ');
  }

  protected value(key: string): FieldValue {
    return this.values()[key] ?? { text: '', number: null, boolean: null, options: [] };
  }

  protected setValue(key: string, patch: Partial<FieldValue>): void {
    this.values.update((v) => ({ ...v, [key]: { ...this.value(key), ...patch } }));
  }

  protected toggleOption(f: SchemaField, option: string, checked: boolean): void {
    const current = this.value(f.attribute.key).options;
    const options = f.attribute.type === 'SingleChoice'
      ? (checked ? [option] : [])
      : checked ? [...current, option] : current.filter((o) => o !== option);
    this.setValue(f.attribute.key, { options });
  }

  // -- Variants --------------------------------------------------------------------------
  protected toggleAxis(key: string, on: boolean): void {
    this.axes.update((a) => (on ? (a.length < MAX_AXES ? [...a, key] : a) : a.filter((x) => x !== key)));
  }

  protected axisFull(key: string): boolean {
    return !this.axes().includes(key) && this.axes().length >= MAX_AXES;
  }

  protected toggleAxisOption(axis: string, option: string, on: boolean): void {
    this.axisOptions.update((all) => {
      const list = all[axis] ?? [];
      return { ...all, [axis]: on ? [...list, option] : list.filter((o) => o !== option) };
    });
  }

  protected row(combo: Record<string, string>): VariantRow {
    return this.rows()[comboKey(combo)] ?? { id: null, stock: 0, price: null, sku: '' };
  }

  protected setRow(combo: Record<string, string>, patch: Partial<VariantRow>): void {
    const key = comboKey(combo);
    this.rows.update((r) => ({ ...r, [key]: { ...this.row(combo), ...patch } }));
  }

  // -- Images ----------------------------------------------------------------------------
  protected addImage(image: { id: string; url: string } | null): void {
    if (image && this.images().length < MAX_IMAGES) this.images.update((list) => [...list, { id: image.id, url: image.url }]);
  }

  protected moveImage(index: number, step: -1 | 1): void {
    this.images.update((list) => {
      const target = index + step;
      if (target < 0 || target >= list.length) return list;
      const copy = [...list];
      [copy[index], copy[target]] = [copy[target], copy[index]];
      return copy;
    });
  }

  protected removeImage(index: number): void {
    this.images.update((list) => list.filter((_, i) => i !== index));
  }

  // -- Save ------------------------------------------------------------------------------
  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.touched && control.invalid;
  }

  protected submit(): void {
    this.saved.set(false);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('myStore.fixErrors');
      return;
    }
    const v = this.form.getRawValue();
    const fieldValues: ValueRequest[] = this.productFields().map((f): ValueRequest => {
      const val = this.value(f.attribute.key);
      switch (f.attribute.type) {
        case 'Text': return { key: f.attribute.key, text: val.text.trim() || null };
        case 'Number': return { key: f.attribute.key, number: val.number };
        case 'Boolean': return { key: f.attribute.key, boolean: val.boolean };
        default: return { key: f.attribute.key, options: val.options };
      }
    });
    const body: SaveProduct = {
      name: v.name.trim(), slug: v.slug.trim() || null, categoryId: v.categoryId, description: v.description.trim() || null,
      price: v.price, compareAtPrice: v.compareAtPrice || null, sku: v.sku.trim() || null, stock: v.stock,
      affiliatePercent: v.affiliatePercent ?? null, status: v.status,
      imageIds: this.images().map((i) => i.id),
      values: fieldValues,
      variantAxes: this.axes(),
      variants: this.combos().map((combo) => {
        const r = this.row(combo);
        return { id: r.id, options: combo, stock: r.stock, price: r.price || null, sku: r.sku.trim() || null };
      }),
    };

    this.saving.set(true);
    this.error.set(null);
    const id = this.id();
    (id ? this.api.update(id, body) : this.api.create(body)).subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.saved.set(true);
        if (id) this.product.set(saved);
        else this.router.navigate(['/my-store/products', saved.id]);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.saving.set(false);
      },
    });
  }

  protected remove(): void {
    const id = this.id();
    if (!id) return;
    this.saving.set(true);
    this.api.delete(id).subscribe({
      next: () => this.router.navigate(['/my-store/products']),
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.saving.set(false);
      },
    });
  }

  /** Loads a saved product into the form (stored values are canonical strings). */
  private fill(p: Product): void {
    this.form.reset({
      name: p.name, slug: p.slug, categoryId: p.category.id, description: p.description ?? '', price: p.price,
      compareAtPrice: p.compareAtPrice, sku: p.sku ?? '', stock: p.stock, affiliatePercent: p.affiliatePercent, status: p.status,
    });
    this.images.set(p.images);
    this.values.set(Object.fromEntries(p.values.map((v) => [v.key, {
      text: v.value,
      number: Number.isFinite(Number(v.value)) ? Number(v.value) : null,
      boolean: v.value === 'true' ? true : v.value === 'false' ? false : null,
      options: v.value.split(',').filter(Boolean),
    }])));
    this.axes.set([...p.variantAxes]);
    const offered: Record<string, string[]> = {};
    for (const axis of p.variantAxes) offered[axis] = [...new Set(p.variants.map((v) => v.options[axis]))];
    this.axisOptions.set(offered);
    this.rows.set(Object.fromEntries(p.variants.map((v) => [comboKey(v.options), { id: v.id, stock: v.stock, price: v.price, sku: v.sku ?? '' }])));
  }
}
