import { DatePipe, formatDate } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import { OWNER_ROLE_REVOKED } from '../../../core/stores/store-api';
import { AuditEntry } from '../admin.models';
import { changedFields, displayValue } from './audit-diff';

@Component({
  selector: 'app-audit-entry-item',
  imports: [DatePipe, RouterLink, TranslocoPipe],
  template: `
    <li class="row-hover flex gap-3 px-5 py-4 sm:px-6">
      <span class="mt-1.5 size-2 shrink-0 rounded-full bg-brand ring-4 ring-brand-soft" aria-hidden="true"></span>
      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-baseline gap-x-2 gap-y-1">
          <span class="text-sm font-semibold text-ink">{{ 'audit.actions.' + entry().action | transloco }}</span>
          @if (showTarget()) {
            @if (entry().targetType === 'User') {
              <a [routerLink]="['/admin/users', entry().targetId]" class="link text-sm" dir="ltr">{{ entry().targetLabel }}</a>
            } @else if (entry().targetType === 'ProductCategory') {
              <a routerLink="/admin/product-categories" [queryParams]="{ id: entry().targetId }" class="link text-sm">{{ entry().targetLabel }}</a>
            } @else if (entry().targetType === 'Order') {
              <a [routerLink]="['/admin/orders', entry().targetLabel]" class="link text-sm" dir="ltr">{{ entry().targetLabel }}</a>
            } @else if (entry().targetType === 'Payout') {
              <a [routerLink]="['/admin/payouts', entry().targetId]" class="link text-sm">{{ entry().targetLabel }}</a>
            } @else if (entry().targetType === 'Store') {
              <a [routerLink]="['/admin/stores', entry().targetId]" class="link text-sm">{{ entry().targetLabel }}</a>
            } @else if (entry().targetType === 'Product') {
              <a routerLink="/admin/products" [queryParams]="{ search: entry().targetLabel }" class="link text-sm">{{ entry().targetLabel }}</a>
            } @else if (entry().targetType === 'Setting') {
              <a routerLink="/admin/settings" class="link text-sm">{{ 'settings.' + entry().targetLabel + '.label' | transloco }}</a>
            } @else {
              <span class="text-sm text-muted" dir="ltr">{{ entry().targetLabel }}</span>
            }
          }
          <span class="ms-auto text-xs whitespace-nowrap text-muted tabular-nums">{{ entry().createdAt | date: 'medium' : undefined : language.locale() }}</span>
        </div>
        <p class="mt-1 text-sm text-muted">
          {{ 'audit.by' | transloco }} <span class="font-medium text-ink-soft" dir="ltr">{{ entry().actorEmail }}</span>
          @if (entry().reason) {
            · {{ 'audit.reason' | transloco }}: <span class="text-ink">{{ reasonLabel() | transloco }}</span>
          }
        </p>
        @if (changes().length) {
          <ul class="mt-3 divide-y divide-line overflow-hidden rounded-lg border border-line bg-subtle/40 text-sm">
            @for (c of changes(); track c.field) {
              <li class="grid gap-1.5 px-3 py-2 sm:grid-cols-[minmax(0,10rem)_1fr] sm:items-center sm:gap-3">
                <span class="eyebrow truncate">{{ 'audit.fields.' + c.field | transloco }}</span>
                <div class="flex min-w-0 flex-wrap items-center gap-2">
                  <span class="min-w-0 break-words rounded-md bg-danger/8 px-1.5 py-0.5 font-mono text-xs text-danger line-through decoration-danger/50">{{ show(c.field, c.before) }}</span>
                  <span aria-hidden="true" class="inline-block text-muted rtl:rotate-180">→</span>
                  <span class="min-w-0 break-words rounded-md bg-success/10 px-1.5 py-0.5 font-mono text-xs font-medium text-success">{{ show(c.field, c.after) }}</span>
                </div>
              </li>
            }
          </ul>
        }
      </div>
    </li>
  `,
})
export class AuditEntryItem {
  readonly entry = input.required<AuditEntry>();
  readonly showTarget = input(true);
  protected readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  protected readonly changes = computed(() => changedFields(this.entry().before, this.entry().after));
  /** Role grants/revokes and approved role requests store the role name as reason ("Admin"), which we translate; free text passes through. */
  protected readonly reasonLabel = computed(() => {
    const { action, reason } = this.entry();
    if (action.startsWith('user.role.') || action === 'roleRequest.approve') return `roles.${reason}`;
    if (action.endsWith('.move') && (reason === 'up' || reason === 'down')) return `admin.moveReason.${reason}`;
    if (reason === OWNER_ROLE_REVOKED) return 'moderation.ownerRoleRevoked';
    return reason ?? '';
  });

  /** Reads language.locale(), so labels re-render when the user switches language. */
  protected show(field: string, value: unknown): string {
    const locale = this.language.locale();
    return displayValue(field, value, (k) => this.transloco.translate(k), (iso) => formatDate(iso, 'medium', locale));
  }
}
