import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { errorKey } from '../../../core/http/problem';
import { LanguageService } from '../../../core/i18n/language.service';
import { formatRib, isOpenPayout, Payout, PayoutApi, PayoutMethod, payoutTone, RIB_LENGTH } from '../../../core/payouts/payout-api';
import { PayoutTimeline } from '../../../shared/payouts/payout-timeline';
import { Modal } from '../../../shared/ui/modal';

/**
 * The "Retraits" tab of the wallet (021): how far the affiliate is from the minimum, the request form (cash or bank
 * transfer), the open request with its steps and planned date, and every past request. Coins are never withdrawn
 * directly — the platform verifies, schedules and pays.
 */
@Component({
  selector: 'app-wallet-payouts',
  host: { class: 'block space-y-6' },
  imports: [DatePipe, DecimalPipe, TranslocoPipe, Modal, PayoutTimeline],
  template: `
    @if (data.value(); as d) {
      <section class="card overflow-hidden">
        <div class="card-header flex-wrap gap-3">
          <div class="min-w-0">
            <h2 class="section-title">{{ 'payouts.mine.title' | transloco }}</h2>
            <p class="mt-0.5 text-sm text-muted">{{ 'payouts.mine.hint' | transloco }}</p>
          </div>
          <button type="button" (click)="openForm()" [disabled]="!canAsk()" class="btn btn-primary shrink-0">{{ 'payouts.mine.ask' | transloco }}</button>
        </div>
        <div class="p-5 sm:p-6">
          <div class="flex flex-wrap items-baseline justify-between gap-2 text-sm">
            <span class="font-semibold text-ink" dir="ltr">{{ d.available | number: '1.0-3' : language.locale() }} / {{ d.minimum | number: '1.0-3' : language.locale() }} {{ 'settings.units.coins' | transloco }}</span>
            @if (open()) {
              <span class="text-muted">{{ 'payouts.mine.oneOpen' | transloco }}</span>
            } @else if (missing() > 0) {
              <span class="text-muted">{{ 'payouts.mine.missing' | transloco: { coins: (missing() | number: '1.0-3' : language.locale()) } }}</span>
            } @else {
              <span class="font-medium text-success">{{ 'payouts.mine.ready' | transloco }}</span>
            }
          </div>
          <div class="mt-2 h-2 overflow-hidden rounded-full bg-subtle" aria-hidden="true">
            <div class="h-full rounded-full transition-all duration-500" [class]="missing() > 0 ? 'bg-accent' : 'bg-success'" [style.width.%]="progress()"></div>
          </div>
          <p class="mt-2 text-xs text-muted">{{ 'payouts.rate' | transloco: { value: (d.coinValueInTnd | number: '1.0-3' : language.locale()) } }}</p>
        </div>
      </section>

      @if (error(); as key) { <p role="alert" class="alert-danger">{{ key | transloco }}</p> }

      @if (open(); as p) {
        <section class="card overflow-hidden ring-1 ring-brand/20">
          <div class="card-header flex-wrap gap-3">
            <div class="min-w-0">
              <p class="flex flex-wrap items-center gap-2">
                <span class="pill" [class]="tone(p.status)">{{ 'payouts.status.' + p.status | transloco }}</span>
                <span class="font-mono text-xs text-ink-soft" dir="ltr">{{ p.reference }}</span>
              </p>
              <p class="mt-1 text-2xl font-bold text-ink" dir="ltr">{{ p.amountTnd | number: '1.0-3' : language.locale() }} <span class="text-sm text-muted">TND</span></p>
              <p class="text-xs text-muted">{{ 'payouts.method.' + p.method | transloco }} · <span dir="ltr">{{ p.coins | number: '1.0-3' : language.locale() }}</span> {{ 'settings.units.coins' | transloco }}</p>
            </div>
            @if (p.status === 'Requested' || p.status === 'Verified') {
              <button type="button" (click)="cancelling.set(p)" class="btn btn-ghost btn-sm shrink-0">{{ 'payouts.mine.cancel' | transloco }}</button>
            }
          </div>
          <div class="grid gap-5 p-5 sm:p-6 md:grid-cols-2">
            <div class="space-y-2 text-sm">
              <p class="text-ink-soft">{{ 'payouts.next.' + p.status + '.' + p.method | transloco }}</p>
              @if (p.scheduledFor) {
                <p class="rounded-lg bg-brand-soft px-3 py-2 font-medium text-brand">
                  {{ p.scheduledFor | date: (p.method === 'Cash' ? 'full' : 'fullDate') : undefined : language.locale() }}
                  @if (p.place) { <br /><span class="font-normal">{{ p.place }}</span> }
                </p>
              }
              @if (p.method === 'BankTransfer') {
                <p class="text-xs text-muted">{{ p.bankName }} · {{ p.accountHolder }} · <span dir="ltr">{{ rib(p.rib) }}</span></p>
              }
            </div>
            <app-payout-timeline [history]="p.history" />
          </div>
        </section>
      }

      <section class="card overflow-hidden">
        <div class="card-header"><h2 class="section-title">{{ 'payouts.mine.history' | transloco }}</h2></div>
        @if (past().length === 0) {
          <p class="empty-state py-8">{{ 'payouts.mine.none' | transloco }}</p>
        } @else {
          <ul class="divide-y divide-line">
            @for (p of past(); track p.id) {
              <li>
                <button type="button" (click)="toggle(p.id)" [attr.aria-expanded]="expanded() === p.id"
                  class="row-hover grid w-full gap-2 px-5 py-4 text-start text-sm sm:grid-cols-[1fr_auto] sm:items-center sm:px-6">
                  <span class="min-w-0">
                    <span class="flex flex-wrap items-center gap-2">
                      <span class="pill" [class]="tone(p.status)">{{ 'payouts.status.' + p.status | transloco }}</span>
                      <span class="font-mono text-xs text-ink-soft" dir="ltr">{{ p.reference }}</span>
                      <span class="text-xs text-muted">{{ p.createdAt | date: 'mediumDate' : undefined : language.locale() }} · {{ 'payouts.method.' + p.method | transloco }}</span>
                    </span>
                    @if (p.rejectionReason) { <span class="mt-1 block text-xs text-danger">{{ p.rejectionReason }}</span> }
                    @if (p.paidAt) { <span class="mt-1 block text-xs text-muted">{{ 'payouts.mine.paidOn' | transloco: { date: (p.paidAt | date: 'mediumDate' : undefined : language.locale()) } }}</span> }
                  </span>
                  <span class="font-bold sm:text-end" [class]="p.status === 'Paid' ? 'text-success' : 'text-muted line-through'" dir="ltr">
                    {{ p.amountTnd | number: '1.0-3' : language.locale() }} TND</span>
                </button>
                @if (expanded() === p.id) {
                  <div class="border-t border-line bg-subtle/40 px-5 py-4 sm:px-6"><app-payout-timeline [history]="p.history" /></div>
                }
              </li>
            }
          </ul>
        }
      </section>
    } @else if (data.error()) {
      <p class="alert-danger">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <p class="card empty-state">{{ 'common.loading' | transloco }}</p>
    }

    @if (asking()) {
      <app-modal [label]="'payouts.form.title' | transloco" (close)="asking.set(false)">
        <h2 class="section-title">{{ 'payouts.form.title' | transloco }}</h2>
        <p class="mt-1 text-sm text-muted">{{ 'payouts.form.hint' | transloco }}</p>

        <label for="poCoins" class="field-label mt-4">{{ 'payouts.form.coins' | transloco }}</label>
        <input id="poCoins" type="number" step="0.001" [min]="data.value()!.minimum" [max]="data.value()!.available" [value]="coins()"
          (input)="coins.set($any($event.target).value)" class="mt-1.5 field" dir="ltr" />
        <p class="field-hint" dir="ltr">≈ {{ amount() | number: '1.0-3' : language.locale() }} TND ·
          {{ 'payouts.form.range' | transloco: { min: data.value()!.minimum, max: (data.value()!.available | number: '1.0-3' : language.locale()) } }}</p>

        <fieldset class="mt-4">
          <legend class="field-label">{{ 'payouts.form.method' | transloco }}</legend>
          <div class="mt-1.5 grid grid-cols-2 gap-2">
            @for (m of methods; track m) {
              <label class="flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2.5 text-sm transition-all duration-200"
                [class]="method() === m ? 'border-brand bg-brand-soft text-brand' : 'border-line hover:border-brand/40'">
                <input type="radio" name="poMethod" [value]="m" [checked]="method() === m" (change)="method.set(m)" class="accent-brand" />
                {{ 'payouts.method.' + m | transloco }}
              </label>
            }
          </div>
        </fieldset>

        @if (method() === 'BankTransfer') {
          <label for="poBank" class="field-label mt-4">{{ 'payouts.form.bankName' | transloco }}</label>
          <input id="poBank" type="text" maxlength="100" [value]="bankName()" (input)="bankName.set($any($event.target).value)" class="mt-1.5 field" autocomplete="off" />
          <label for="poHolder" class="field-label mt-4">{{ 'payouts.form.accountHolder' | transloco }}</label>
          <input id="poHolder" type="text" maxlength="150" [value]="accountHolder()" (input)="accountHolder.set($any($event.target).value)" class="mt-1.5 field" autocomplete="name" />
          <label for="poRib" class="field-label mt-4">{{ 'payouts.form.rib' | transloco }}</label>
          <input id="poRib" type="text" inputmode="numeric" maxlength="30" [value]="ribInput()" (input)="ribInput.set($any($event.target).value)" class="mt-1.5 field font-mono" dir="ltr" autocomplete="off" />
          <p class="field-hint" [class.text-danger]="ribInput() && !ribOk()">{{ 'payouts.form.ribHint' | transloco: { count: ribDigits().length } }}</p>
        } @else {
          <p class="alert-info mt-4">{{ 'payouts.form.cashHint' | transloco }}</p>
        }

        <label for="poNote" class="field-label mt-4">{{ 'payouts.form.note' | transloco }}</label>
        <textarea id="poNote" rows="2" maxlength="500" [value]="note()" (input)="note.set($any($event.target).value)" class="mt-1.5 field resize-y"
          [placeholder]="'payouts.form.notePlaceholder' | transloco"></textarea>

        @if (formError(); as key) { <p role="alert" class="alert-danger mt-4">{{ key | transloco }}</p> }
        <div class="mt-6 flex justify-end gap-3">
          <button type="button" (click)="asking.set(false)" class="btn btn-ghost">{{ 'common.cancel' | transloco }}</button>
          <button type="button" (click)="send()" [disabled]="busy() || !formReady()" class="btn btn-primary">{{ 'payouts.form.send' | transloco }}</button>
        </div>
      </app-modal>
    }

    @if (cancelling(); as p) {
      <app-modal [label]="'payouts.mine.cancel' | transloco" (close)="cancelling.set(null)">
        <h2 class="section-title">{{ 'payouts.mine.cancelTitle' | transloco: { reference: p.reference } }}</h2>
        <p class="mt-2 text-sm text-muted">{{ 'payouts.mine.cancelHint' | transloco }}</p>
        <div class="mt-6 flex justify-end gap-3">
          <button type="button" (click)="cancelling.set(null)" class="btn btn-ghost">{{ 'common.no' | transloco }}</button>
          <button type="button" (click)="cancel(p)" [disabled]="busy()" class="btn btn-danger">{{ 'payouts.mine.cancel' | transloco }}</button>
        </div>
      </app-modal>
    }
  `,
})
export class WalletPayouts {
  private readonly api = inject(PayoutApi);
  protected readonly language = inject(LanguageService);
  /** Coins moved (locked or released): the wallet reloads its counters. */
  readonly changed = output<void>();

  protected readonly data = rxResource({ stream: () => this.api.mine() });
  protected readonly open = computed(() => this.data.value()?.items.find((p) => isOpenPayout(p.status)) ?? null);
  protected readonly past = computed(() => (this.data.value()?.items ?? []).filter((p) => !isOpenPayout(p.status)));
  protected readonly missing = computed(() => {
    const d = this.data.value();
    return d ? Math.max(0, d.minimum - d.available) : 0;
  });
  protected readonly progress = computed(() => {
    const d = this.data.value();
    return !d || d.minimum <= 0 ? 100 : Math.min(100, (d.available / d.minimum) * 100);
  });
  protected readonly canAsk = computed(() => {
    const d = this.data.value();
    return !!d && !this.open() && d.available > 0 && d.available >= d.minimum;
  });

  protected readonly methods: PayoutMethod[] = ['Cash', 'BankTransfer'];
  protected readonly tone = payoutTone;
  protected readonly rib = formatRib;

  protected readonly asking = signal(false);
  protected readonly cancelling = signal<Payout | null>(null);
  protected readonly expanded = signal<string | null>(null);
  protected readonly coins = signal('');
  protected readonly method = signal<PayoutMethod>('Cash');
  protected readonly bankName = signal('');
  protected readonly accountHolder = signal('');
  protected readonly ribInput = signal('');
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);

  protected readonly ribDigits = computed(() => this.ribInput().replace(/[\s.-]/g, ''));
  protected readonly ribOk = computed(() => new RegExp(`^\\d{${RIB_LENGTH}}$`).test(this.ribDigits()));
  protected readonly amount = computed(() => (Number(this.coins()) || 0) * (this.data.value()?.coinValueInTnd ?? 0));
  protected readonly formReady = computed(() => {
    const d = this.data.value();
    const n = Number(this.coins());
    if (!d || !n || n < d.minimum || n > d.available) return false;
    return this.method() === 'Cash' || (!!this.bankName().trim() && !!this.accountHolder().trim() && this.ribOk());
  });

  protected openForm(): void {
    const d = this.data.value()!;
    this.coins.set(String(d.available));
    this.method.set(d.lastBankDetails ? 'BankTransfer' : 'Cash');
    this.bankName.set(d.lastBankDetails?.bankName ?? '');
    this.accountHolder.set(d.lastBankDetails?.accountHolder ?? '');
    this.ribInput.set(d.lastBankDetails ? formatRib(d.lastBankDetails.rib) : '');
    this.note.set('');
    this.formError.set(null);
    this.asking.set(true);
  }

  protected toggle(id: string): void {
    this.expanded.update((current) => (current === id ? null : id));
  }

  protected send(): void {
    this.busy.set(true);
    this.formError.set(null);
    const transfer = this.method() === 'BankTransfer';
    this.api.request({
      coins: Number(this.coins()),
      method: this.method(),
      bankName: transfer ? this.bankName().trim() : null,
      accountHolder: transfer ? this.accountHolder().trim() : null,
      rib: transfer ? this.ribDigits() : null,
      note: this.note().trim() || null,
    }).subscribe({
      next: () => this.done(() => this.asking.set(false)),
      error: (err: unknown) => {
        this.busy.set(false);
        this.formError.set(errorKey(err));
      },
    });
  }

  protected cancel(p: Payout): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.cancel(p.id).subscribe({
      next: () => this.done(() => this.cancelling.set(null)),
      error: (err: unknown) => {
        this.busy.set(false);
        this.cancelling.set(null);
        this.error.set(errorKey(err));
        this.data.reload();
      },
    });
  }

  private done(close: () => void): void {
    this.busy.set(false);
    close();
    this.data.reload();
    this.changed.emit();
  }
}
