import { DecimalPipe } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { StatsRow } from '../../core/stats/stats-api';
import { downloadCsv, toCsv } from '../csv';

/**
 * A ranking or breakdown of a stats screen (023) with its CSV export (built here, in the active language).
 * `countKey` names the count column: delivered orders for stores / products, credited lines for affiliates.
 */
@Component({
  selector: 'app-stats-table',
  // min-w-0: inside a grid, a wide table scrolls in its card instead of widening the page.
  host: { class: 'block min-w-0' },
  imports: [DecimalPipe, RouterLink, TranslocoPipe],
  template: `
    <section class="card overflow-hidden">
      <div class="card-header">
        <h2 class="section-title">{{ title() }}</h2>
        @if (rows().length) {
          <button type="button" class="btn btn-ghost btn-sm" (click)="download()">{{ 'stats.csv' | transloco }}</button>
        }
      </div>
      @if (rows().length === 0) {
        <p class="empty-state">{{ 'stats.noData' | transloco }}</p>
      } @else {
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-subtle/60 text-xs text-muted">
              <tr>
                <th scope="col" class="px-4 py-2 text-start font-medium">{{ nameKey() | transloco }}</th>
                <th scope="col" class="px-4 py-2 text-end font-medium">{{ countKey() | transloco }}</th>
                <th scope="col" class="hidden px-4 py-2 text-end font-medium sm:table-cell">{{ 'stats.col.quantity' | transloco }}</th>
                <th scope="col" class="px-4 py-2 text-end font-medium">{{ 'stats.col.amount' | transloco }}</th>
                @if (showCoins()) { <th scope="col" class="px-4 py-2 text-end font-medium">{{ 'stats.col.coins' | transloco }}</th> }
              </tr>
            </thead>
            <tbody class="divide-y divide-line">
              @for (r of rows(); track r.id) {
                <tr class="row-hover">
                  <td class="max-w-48 px-4 py-2.5 sm:max-w-none">
                    @if (linkBase()) {
                      <a [routerLink]="[linkBase(), r.id]" class="link block truncate font-medium">{{ r.name }}</a>
                    } @else {
                      <span class="block truncate font-medium text-ink">{{ r.name }}</span>
                    }
                    @if (r.detail) { <span class="block truncate text-xs text-muted" [attr.dir]="detailLtr() ? 'ltr' : null">{{ r.detail }}</span> }
                  </td>
                  <td class="px-4 py-2.5 text-end tabular-nums" dir="ltr">{{ r.count | number: '1.0-0' : language.locale() }}</td>
                  <td class="hidden px-4 py-2.5 text-end tabular-nums sm:table-cell" dir="ltr">{{ r.quantity | number: '1.0-0' : language.locale() }}</td>
                  <td class="px-4 py-2.5 text-end tabular-nums whitespace-nowrap" dir="ltr">{{ r.amount | number: '1.0-3' : language.locale() }}</td>
                  @if (showCoins()) {
                    <td class="px-4 py-2.5 text-end tabular-nums" dir="ltr">{{ r.coins | number: '1.0-3' : language.locale() }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
})
export class StatsTable {
  private readonly transloco = inject(TranslocoService);
  protected readonly language = inject(LanguageService);

  readonly title = input.required<string>();
  readonly rows = input.required<StatsRow[]>();
  /** Header of the name column (`stats.col.store`, `stats.col.product`, `stats.col.affiliate`). */
  readonly nameKey = input.required<string>();
  readonly countKey = input('stats.col.orders');
  readonly showCoins = input(true);
  /** Row link: `[linkBase, row.id]`; none when null (the store owner's view). */
  readonly linkBase = input<string | null>(null);
  /** The detail is an affiliate code (left to right). */
  readonly detailLtr = input(false);
  readonly csvName = input('stats');

  protected download(): void {
    const t = (key: string) => this.transloco.translate(key);
    const headers = [t(this.nameKey()), t('stats.col.detail'), t(this.countKey()), t('stats.col.quantity'), t('stats.col.amount')];
    if (this.showCoins()) headers.push(t('stats.col.coins'));
    const rows = this.rows().map((r) => {
      const row: (string | number | null)[] = [r.name, r.detail, r.count, r.quantity, r.amount];
      if (this.showCoins()) row.push(r.coins);
      return row;
    });
    downloadCsv(toCsv(headers, rows), `${this.csvName()}-${new Date().toISOString().slice(0, 10)}.csv`);
  }
}
