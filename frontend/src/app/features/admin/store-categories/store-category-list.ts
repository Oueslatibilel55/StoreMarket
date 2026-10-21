import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { ImageUpload } from '../../../shared/ui/image-upload';
import { Modal } from '../../../shared/ui/modal';
import { AdminApi } from '../admin-api';
import { AdminStoreCategory } from '../admin.models';

/** Same rule as the backend (StoreCategory.IsValidSlug): lower-case words joined by dashes. */
const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;
/** A few ready-made icons; any emoji can be typed. */
const ICONS = ['🌸', '👗', '💄', '👟', '💍', '📱', '🏠', '🍰', '🧸', '⚽', '📚', '🛒'];

type Pending =
  | { kind: 'form'; category: AdminStoreCategory | null }
  | { kind: 'toggle' | 'delete'; category: AdminStoreCategory };

@Component({
  selector: 'app-store-category-list',
  imports: [ReactiveFormsModule, TranslocoPipe, LocalizedPipe, Modal, ImageUpload],
  templateUrl: './store-category-list.html',
})
export class StoreCategoryList {
  private readonly api = inject(AdminApi);
  protected readonly language = inject(LanguageService);

  protected readonly categories = rxResource({ stream: () => this.api.storeCategories() });
  protected readonly icons = ICONS;

  protected readonly form = inject(NonNullableFormBuilder).group({
    nameFr: ['', [Validators.required, Validators.maxLength(60)]],
    nameAr: ['', Validators.maxLength(60)],
    nameEn: ['', Validators.maxLength(60)],
    slug: ['', [Validators.maxLength(60), Validators.pattern(SLUG_PATTERN)]],
    icon: ['', Validators.maxLength(16)],
  });

  /** Image of the form being edited (uploaded at once, saved with the form). */
  protected readonly image = signal<{ id: string; url: string } | null>(null);
  protected readonly pending = signal<Pending | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected openForm(category: AdminStoreCategory | null): void {
    this.form.reset({
      nameFr: category?.name.fr ?? '',
      nameAr: category?.name.ar ?? '',
      nameEn: category?.name.en ?? '',
      slug: category?.slug ?? '',
      icon: category?.icon ?? '',
    });
    this.image.set(category?.imageId && category.imageUrl ? { id: category.imageId, url: category.imageUrl } : null);
    this.open({ kind: 'form', category });
  }

  protected open(action: Pending): void {
    this.error.set(null);
    this.pending.set(action);
  }

  protected close(): void {
    if (!this.busy()) this.pending.set(null);
  }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.touched && control.invalid;
  }

  protected save(): void {
    const action = this.pending();
    if (action?.kind !== 'form') return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      nameFr: v.nameFr.trim(),
      nameAr: v.nameAr.trim() || null,
      nameEn: v.nameEn.trim() || null,
      slug: v.slug.trim() || null,
      icon: v.icon.trim() || null,
      imageId: this.image()?.id ?? null,
    };
    this.run(action.category ? this.api.updateStoreCategory(action.category.id, body) : this.api.createStoreCategory(body));
  }

  protected confirm(): void {
    const action = this.pending();
    if (action?.kind === 'toggle') this.run(this.api.setStoreCategoryActive(action.category.id, !action.category.isActive));
    if (action?.kind === 'delete') this.run(this.api.deleteStoreCategory(action.category.id));
  }

  protected move(category: AdminStoreCategory, direction: 'up' | 'down'): void {
    this.api.moveStoreCategory(category.id, direction).subscribe({
      next: () => this.categories.reload(),
      error: (err: unknown) => this.error.set(errorKey(err)),
    });
  }

  private run(call: Observable<unknown>): void {
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.pending.set(null);
        this.categories.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
