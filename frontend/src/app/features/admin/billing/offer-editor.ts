import { Component, computed, inject, input, OnInit, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { of } from 'rxjs';
import { BillingApi, Offer, OfferTarget, SaveOffer } from '../../../core/billing/billing-api';
import { CatalogApi } from '../../../core/catalog/catalog-api';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { LocalizedPipe } from '../../../shared/i18n/localized';
import { Modal } from '../../../shared/ui/modal';
import { ModerationApi } from '../moderation/moderation-api';

const TARGETS: OfferTarget[] = ['AllStores', 'StoreCategories', 'Stores'];

/** Creates or edits a subscription offer (025): names, months, % or fixed TND, dates, which stores, first payment only, active. */
@Component({
  selector: 'app-offer-editor',
  imports: [TranslocoPipe, LocalizedPipe, Modal],
  template: `
    <app-modal [label]="'billing.offers.edit' | transloco" (close)="closed.emit()">
      <h2 class="section-title">{{ (offer() ? 'billing.offers.edit' : 'billing.offers.new') | transloco }}</h2>
      <div class="mt-4 grid gap-3">
        <label class="block"><span class="field-label">{{ 'billing.offers.nameFr' | transloco }}</span>
          <input class="field" maxlength="100" [value]="nameFr()" (input)="nameFr.set($any($event.target).value)" /></label>
        <div class="grid gap-3 sm:grid-cols-2">
          <label class="block"><span class="field-label">{{ 'billing.offers.nameAr' | transloco }}</span>
            <input class="field" dir="rtl" maxlength="100" [value]="nameAr()" (input)="nameAr.set($any($event.target).value)" /></label>
          <label class="block"><span class="field-label">{{ 'billing.offers.nameEn' | transloco }}</span>
            <input class="field" dir="ltr" maxlength="100" [value]="nameEn()" (input)="nameEn.set($any($event.target).value)" /></label>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <label class="block"><span class="field-label">{{ 'billing.offers.months' | transloco }}</span>
            <input type="number" min="1" max="36" class="field" dir="ltr" [value]="months()" [placeholder]="'billing.offers.anyMonths' | transloco"
              (input)="months.set($any($event.target).value)" /></label>
          <div>
            <span class="field-label">{{ 'billing.offers.discount' | transloco }}</span>
            <div class="flex gap-2">
              <input type="number" min="0" step="0.001" class="field min-w-0" dir="ltr" [value]="discount()" (input)="discount.set($any($event.target).value)" />
              <select class="field w-auto" [value]="kind()" (change)="kind.set($any($event.target).value)">
                <option value="percent" [selected]="kind() === 'percent'">%</option>
                <option value="amount" [selected]="kind() === 'amount'">TND</option>
              </select>
            </div>
          </div>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <label class="block"><span class="field-label">{{ 'billing.offers.startsOn' | transloco }}</span>
            <input type="date" class="field" dir="ltr" [value]="startsOn()" (change)="startsOn.set($any($event.target).value)" /></label>
          <label class="block"><span class="field-label">{{ 'billing.offers.endsOn' | transloco }}</span>
            <input type="date" class="field" dir="ltr" [value]="endsOn()" (change)="endsOn.set($any($event.target).value)" /></label>
        </div>
        <label class="block"><span class="field-label">{{ 'billing.offers.target' | transloco }}</span>
          <select class="field" [value]="target()" (change)="setTarget($any($event.target).value)">
            @for (t of targets; track t) { <option [value]="t" [selected]="t === target()">{{ 'billing.offers.targets.' + t | transloco }}</option> }
          </select></label>

        @if (target() === 'StoreCategories') {
          <div class="flex flex-wrap gap-2">
            @for (c of categories.value() ?? []; track c.id) {
              <button type="button" (click)="toggle(c.id)" [attr.aria-pressed]="ids().includes(c.id)"
                class="cursor-pointer rounded-full border px-3 py-1 text-sm transition-colors"
                [class]="ids().includes(c.id) ? 'border-brand bg-brand-soft text-brand' : 'border-line text-ink-soft hover:border-brand'">
                {{ c.name | localized: language.lang() }}
              </button>
            }
          </div>
        }
        @if (target() === 'Stores') {
          <input type="search" class="field" [placeholder]="'billing.offers.searchStores' | transloco" (input)="storeSearch.set($any($event.target).value)" />
          <div class="flex max-h-40 flex-wrap gap-2 overflow-y-auto">
            @for (s of stores.value()?.items ?? []; track s.id) {
              <button type="button" (click)="toggle(s.id)" [attr.aria-pressed]="ids().includes(s.id)"
                class="cursor-pointer rounded-full border px-3 py-1 text-sm transition-colors"
                [class]="ids().includes(s.id) ? 'border-brand bg-brand-soft text-brand' : 'border-line text-ink-soft hover:border-brand'">
                {{ s.name }}
              </button>
            }
          </div>
          <p class="text-xs text-muted">{{ 'billing.offers.selected' | transloco: { n: ids().length } }}</p>
        }

        <label class="flex items-center gap-2 text-sm"><input type="checkbox" class="checkbox" [checked]="firstOnly()" (change)="firstOnly.set($any($event.target).checked)" />
          {{ 'billing.offers.firstPaymentOnly' | transloco }}</label>
        <label class="flex items-center gap-2 text-sm"><input type="checkbox" class="checkbox" [checked]="active()" (change)="active.set($any($event.target).checked)" />
          {{ 'billing.offers.active' | transloco }}</label>
      </div>
      @if (error()) { <p class="alert-danger mt-3">{{ error()! | transloco }}</p> }
      <div class="mt-5 flex justify-end gap-2">
        <button type="button" class="btn btn-ghost" (click)="closed.emit()">{{ 'common.cancel' | transloco }}</button>
        <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="save()">{{ 'common.save' | transloco }}</button>
      </div>
    </app-modal>
  `,
})
export class OfferEditor implements OnInit {
  private readonly api = inject(BillingApi);
  private readonly catalog = inject(CatalogApi);
  private readonly moderation = inject(ModerationApi);
  protected readonly language = inject(LanguageService);

  /** Null = a new offer. */
  readonly offer = input<Offer | null>(null);
  readonly saved = output<Offer>();
  readonly closed = output<void>();

  protected readonly targets = TARGETS;
  protected readonly nameFr = signal('');
  protected readonly nameAr = signal('');
  protected readonly nameEn = signal('');
  protected readonly months = signal('');
  protected readonly discount = signal('');
  protected readonly kind = signal<'percent' | 'amount'>('percent');
  protected readonly startsOn = signal('');
  protected readonly endsOn = signal('');
  protected readonly target = signal<OfferTarget>('AllStores');
  protected readonly ids = signal<string[]>([]);
  protected readonly firstOnly = signal(false);
  protected readonly active = signal(true);
  protected readonly storeSearch = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly categories = rxResource({
    params: () => this.target() === 'StoreCategories' || undefined,
    stream: ({ params }) => (params ? this.catalog.storeCategories() : of([])),
  });
  protected readonly stores = rxResource({
    params: () => (this.target() === 'Stores' ? { search: this.storeSearch().trim() } : undefined),
    stream: ({ params }) => (params ? this.moderation.stores({ search: params.search || undefined, pageSize: 50 }) : of(undefined)),
  });
  private readonly body = computed<SaveOffer>(() => {
    const value = this.discount() === '' ? null : Number(this.discount());
    return {
      nameFr: this.nameFr().trim(),
      nameAr: this.nameAr().trim() || null,
      nameEn: this.nameEn().trim() || null,
      months: this.months() === '' ? null : Number(this.months()),
      discountPercent: this.kind() === 'percent' ? value : null,
      discountAmount: this.kind() === 'amount' ? value : null,
      startsOn: this.startsOn() || null,
      endsOn: this.endsOn() || null,
      target: this.target(),
      targetIds: this.target() === 'AllStores' ? [] : this.ids(),
      firstPaymentOnly: this.firstOnly(),
      isActive: this.active(),
    };
  });

  ngOnInit(): void {
    const o = this.offer();
    if (!o) return;
    this.nameFr.set(o.name.fr);
    this.nameAr.set(o.name.ar ?? '');
    this.nameEn.set(o.name.en ?? '');
    this.months.set(o.months === null ? '' : String(o.months));
    this.kind.set(o.discountPercent !== null ? 'percent' : 'amount');
    this.discount.set(String(o.discountPercent ?? o.discountAmount ?? ''));
    this.startsOn.set(o.startsOn ?? '');
    this.endsOn.set(o.endsOn ?? '');
    this.target.set(o.target);
    this.ids.set([...o.targetIds]);
    this.firstOnly.set(o.firstPaymentOnly);
    this.active.set(o.isActive);
  }

  protected setTarget(target: OfferTarget): void {
    this.target.set(target);
    this.ids.set([]);
  }

  protected toggle(id: string): void {
    this.ids.update((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]));
  }

  protected save(): void {
    this.busy.set(true);
    this.error.set(null);
    const o = this.offer();
    (o ? this.api.updateOffer(o.id, this.body()) : this.api.createOffer(this.body())).subscribe({
      next: (saved) => {
        this.busy.set(false);
        this.saved.emit(saved);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(errorKey(err));
      },
    });
  }
}
