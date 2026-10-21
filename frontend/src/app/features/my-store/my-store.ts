import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { catchError, of, throwError } from 'rxjs';
import { CatalogApi } from '../../core/catalog/catalog-api';
import { errorKey } from '../../core/http/problem';
import { LanguageService } from '../../core/i18n/language.service';
import { PlatformSettings } from '../../core/settings/platform-settings';
import { CITIES } from '../../core/stores/cities';
import { MyStore as MyStoreModel, SaveStore, StoreApi } from '../../core/stores/store-api';
import { LocalizedPipe } from '../../shared/i18n/localized';
import { ImageUpload } from '../../shared/ui/image-upload';
import { Modal } from '../../shared/ui/modal';
import { ModerationReason } from '../../shared/ui/moderation-reason';
import { MyStoreTabs } from './my-store-tabs';

/** Same rules as the backend (Store / Slug / User.NormalizePhone). */
const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const PHONE_PATTERN = /^(\+216|00216)?[\s.-]*(\d[\s.-]*){8}$/;
const URL_PATTERN = /^https?:\/\/\S+$/;

interface Picked {
  id: string;
  url: string;
}

/** The store owner's page: create the store the first time, then edit it and open / close it. */
@Component({
  selector: 'app-my-store',
  imports: [ReactiveFormsModule, TranslocoPipe, DatePipe, DecimalPipe, LocalizedPipe, ImageUpload, Modal, MyStoreTabs, ModerationReason],
  templateUrl: './my-store.html',
})
export class MyStore {
  private readonly api = inject(StoreApi);
  private readonly settings = inject(PlatformSettings);
  private readonly catalog = inject(CatalogApi);
  protected readonly language = inject(LanguageService);

  /** null = no store yet (404). */
  protected readonly store = rxResource({
    stream: () => this.api.mine().pipe(
      catchError((err: unknown) => (err instanceof HttpErrorResponse && err.status === 404 ? of(null) : throwError(() => err))),
    ),
  });
  protected readonly categories = rxResource({ stream: () => this.catalog.storeCategories() });

  protected readonly cities = CITIES;
  protected readonly percentMin = computed(() => this.settings.number('affiliatePercent.min', 0));
  protected readonly percentMax = computed(() => this.settings.number('affiliatePercent.max', 50));

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    slug: ['', [Validators.maxLength(60), Validators.pattern(SLUG_PATTERN)]],
    categoryId: ['', Validators.required],
    description: ['', Validators.maxLength(2000)],
    phone: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
    city: ['', Validators.required],
    address: ['', Validators.maxLength(200)],
    defaultAffiliatePercent: [10, [Validators.required, Validators.min(0), Validators.max(100)]],
    deliveryFee: [7, [Validators.required, Validators.min(0), Validators.max(1000)]],
    deliveryMinDays: [1, [Validators.required, Validators.min(0), Validators.max(30)]],
    deliveryMaxDays: [3, [Validators.required, Validators.min(0), Validators.max(30)]],
    facebookUrl: ['', Validators.pattern(URL_PATTERN)],
    instagramUrl: ['', Validators.pattern(URL_PATTERN)],
    websiteUrl: ['', Validators.pattern(URL_PATTERN)],
  });
  protected readonly deliveryCities = signal<string[]>([]);
  protected readonly logo = signal<Picked | null>(null);
  protected readonly banner = signal<Picked | null>(null);

  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly confirmStatus = signal(false);

  constructor() {
    // Fill the form once the store is loaded (and again after each save).
    effect(() => {
      const s = this.store.value();
      if (s) this.fill(s);
    });
  }

  /** Categories to choose from: active ones, plus the store's own even if the admin hid it since. */
  protected readonly categoryOptions = computed(() => {
    const active = this.categories.value() ?? [];
    const own = this.store.value()?.category;
    return own && !active.some((c) => c.id === own.id) ? [...active, { ...own, icon: null, imageUrl: null }] : active;
  });

  protected toggleCity(code: string, checked: boolean): void {
    this.deliveryCities.update((list) => (checked ? [...list, code] : list.filter((c) => c !== code)));
  }

  protected allCities(all: boolean): void {
    this.deliveryCities.set(all ? [...CITIES] : []);
  }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.touched && control.invalid;
  }

  protected percentInvalid(): boolean {
    const p = this.form.controls.defaultAffiliatePercent.value;
    return p < this.percentMin() || p > this.percentMax();
  }

  protected delayInvalid(): boolean {
    return this.form.controls.deliveryMinDays.value > this.form.controls.deliveryMaxDays.value;
  }

  protected submit(): void {
    this.saved.set(false);
    if (this.form.invalid || this.deliveryCities().length === 0 || this.percentInvalid() || this.delayInvalid()) {
      this.form.markAllAsTouched();
      this.error.set(this.deliveryCities().length === 0 ? 'errors.store.deliveryCitiesRequired' : 'myStore.fixErrors');
      return;
    }
    const v = this.form.getRawValue();
    const body: SaveStore = {
      name: v.name.trim(),
      slug: v.slug.trim() || null,
      categoryId: v.categoryId,
      description: v.description.trim() || null,
      phone: v.phone,
      city: v.city,
      address: v.address.trim() || null,
      logoImageId: this.logo()?.id ?? null,
      bannerImageId: this.banner()?.id ?? null,
      defaultAffiliatePercent: v.defaultAffiliatePercent,
      deliveryFee: v.deliveryFee,
      deliveryCities: this.deliveryCities(),
      deliveryMinDays: v.deliveryMinDays,
      deliveryMaxDays: v.deliveryMaxDays,
      facebookUrl: v.facebookUrl.trim() || null,
      instagramUrl: v.instagramUrl.trim() || null,
      websiteUrl: v.websiteUrl.trim() || null,
    };

    this.saving.set(true);
    this.error.set(null);
    (this.store.value() ? this.api.update(body) : this.api.create(body)).subscribe({
      next: (store) => {
        this.store.set(store);
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.saving.set(false);
      },
    });
  }

  protected toggleStatus(): void {
    const store = this.store.value();
    if (!store) return;
    this.saving.set(true);
    (store.status === 'Active' ? this.api.close() : this.api.reopen()).subscribe({
      next: (updated) => {
        this.store.set(updated);
        this.saving.set(false);
        this.confirmStatus.set(false);
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.saving.set(false);
      },
    });
  }

  private fill(s: MyStoreModel): void {
    this.form.reset({
      // The +216 prefix is shown next to the field: edit only the 8 digits.
      name: s.name, slug: s.slug, categoryId: s.category.id, description: s.description ?? '', phone: s.phone.replace(/^\+216/, ''),
      city: s.city, address: s.address ?? '', defaultAffiliatePercent: s.defaultAffiliatePercent, deliveryFee: s.deliveryFee,
      deliveryMinDays: s.deliveryMinDays, deliveryMaxDays: s.deliveryMaxDays, facebookUrl: s.facebookUrl ?? '',
      instagramUrl: s.instagramUrl ?? '', websiteUrl: s.websiteUrl ?? '',
    });
    this.deliveryCities.set([...s.deliveryCities]);
    this.logo.set(s.logoImageId && s.logoUrl ? { id: s.logoImageId, url: s.logoUrl } : null);
    this.banner.set(s.bannerImageId && s.bannerUrl ? { id: s.bannerImageId, url: s.bannerUrl } : null);
  }
}
