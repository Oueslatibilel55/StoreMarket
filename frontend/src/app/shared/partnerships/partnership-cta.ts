import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { errorKey } from '../../core/http/problem';
import { OPEN_STATUSES, PartnershipApi, shareLink } from '../../core/partnerships/partnership-api';

/**
 * For affiliates on a store or product page (012): ask to work with the store, follow the request,
 * and — once the owner accepted — copy the share link of the page. Renders nothing for other users.
 */
@Component({
  selector: 'app-partnership-cta',
  imports: [RouterLink, TranslocoPipe],
  template: `
    @if (code(); as c) {
      @if (current(); as p) {
        @if (p.status === 'Accepted') {
          <div class="space-y-3">
            <p class="flex flex-wrap items-center gap-2 text-sm">
              <span class="pill pill-success"><span class="size-1.5 rounded-full bg-current" aria-hidden="true"></span>{{ 'partnerships.status.Accepted' | transloco }}</span>
              <span class="text-ink-soft">{{ 'partnerships.cta.partner' | transloco }}</span>
            </p>
            <div class="flex flex-wrap gap-2 sm:flex-nowrap">
              <input type="text" readonly [value]="link()" dir="ltr" [attr.aria-label]="'partnerships.cta.shareLink' | transloco"
                class="field min-w-0 flex-1 font-mono text-xs" (focus)="$any($event.target).select()" />
              <button type="button" (click)="copy()" class="btn btn-sm" [class]="copied() ? 'btn-success' : 'btn-secondary'">
                {{ (copied() ? 'affiliate.copied' : 'affiliate.copy') | transloco }}
              </button>
            </div>
            <p class="field-hint">{{ 'partnerships.cta.shareHint' | transloco: { code: '#' + c } }}</p>
          </div>
        } @else {
          <div class="flex flex-wrap items-center justify-between gap-3">
            <p class="flex flex-wrap items-center gap-2 text-sm">
              <span class="pill pill-warning">{{ 'partnerships.status.Requested' | transloco }}</span>
              <span class="text-ink-soft">{{ 'partnerships.cta.waiting' | transloco }}</span>
            </p>
            <button type="button" (click)="withdraw(p.id)" [disabled]="busy()" class="btn btn-ghost btn-sm">{{ 'partnerships.cancel' | transloco }}</button>
          </div>
        }
      } @else if (mine.value()) {
        @if (last(); as l) {
          @if (l.status === 'Refused' || l.status === 'Ended') {
            <div class="mb-3 rounded-lg border-s-2 border-line-strong bg-subtle px-3 py-2 text-xs text-muted">
              <p>{{ 'partnerships.cta.last.' + l.status | transloco }}</p>
              @if (l.decisionReason || l.endReason) { <p class="mt-1 text-ink-soft">{{ l.decisionReason || l.endReason }}</p> }
            </div>
          }
        }
        @if (open()) {
          <div class="space-y-3 animate-fade-in">
            <label for="partnershipMessage" class="field-label">{{ 'partnerships.cta.message' | transloco }}</label>
            <textarea id="partnershipMessage" rows="3" maxlength="500" [value]="message()" (input)="message.set($any($event.target).value)"
              [placeholder]="'partnerships.cta.messagePlaceholder' | transloco" class="field resize-y"></textarea>
            <div class="flex flex-wrap justify-end gap-2">
              <button type="button" (click)="open.set(false)" class="btn btn-ghost btn-sm">{{ 'common.cancel' | transloco }}</button>
              <button type="button" (click)="send()" [disabled]="busy()" class="btn btn-primary btn-sm">{{ 'partnerships.cta.send' | transloco }}</button>
            </div>
          </div>
        } @else {
          <div class="flex flex-wrap items-center justify-between gap-3">
            <p class="text-sm text-ink-soft">{{ 'partnerships.cta.intro' | transloco }}</p>
            <button type="button" (click)="open.set(true)" class="btn btn-primary btn-sm">{{ 'partnerships.cta.ask' | transloco }}</button>
          </div>
        }
      }
      @if (error(); as key) {
        <p role="alert" class="alert-danger mt-3">{{ key | transloco }}</p>
      }
      <a routerLink="/affiliate" class="link mt-3 inline-block text-xs">{{ 'partnerships.cta.allStores' | transloco }}</a>
    }
  `,
})
export class PartnershipCta {
  private readonly api = inject(PartnershipApi);
  private readonly auth = inject(AuthService);

  readonly storeId = input.required<string>();
  /** Page the share link points to, e.g. `/products/musc-blanc`. */
  readonly sharePath = input.required<string>();

  protected readonly code = computed(() => (this.auth.hasRole('Affiliate') ? this.auth.user()?.affiliateCode ?? null : null));
  protected readonly mine = rxResource({
    params: () => (this.code() ? true : undefined),
    stream: () => this.api.listMine(),
  });
  private readonly forStore = computed(() => (this.mine.value() ?? []).filter((p) => p.store.id === this.storeId()));
  /** The requested or accepted partnership with this store. */
  protected readonly current = computed(() => this.forStore().find((p) => OPEN_STATUSES.includes(p.status)) ?? null);
  /** The latest closed one, to show why it stopped. */
  protected readonly last = computed(() => this.forStore()[0] ?? null);
  protected readonly link = computed(() => shareLink(this.sharePath(), this.code() ?? ''));

  protected readonly open = signal(false);
  protected readonly message = signal('');
  protected readonly busy = signal(false);
  protected readonly copied = signal(false);
  protected readonly error = signal<string | null>(null);

  protected send(): void {
    this.run(this.api.request(this.storeId(), this.message().trim() || null), () => {
      this.open.set(false);
      this.message.set('');
    });
  }

  protected withdraw(id: string): void {
    this.run(this.api.cancel(id));
  }

  protected async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.link());
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      // Clipboard blocked: the link stays selectable in the field.
    }
  }

  private run(call: ReturnType<PartnershipApi['cancel']>, done?: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        done?.();
        this.mine.reload();
      },
      error: (err: unknown) => {
        this.error.set(errorKey(err));
        this.busy.set(false);
      },
    });
  }
}
