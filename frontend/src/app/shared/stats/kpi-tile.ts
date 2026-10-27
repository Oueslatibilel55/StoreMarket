import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { Params, RouterLink } from '@angular/router';

/**
 * One dashboard figure (023): `<app-kpi [label]="…" [link]="['/admin/orders']" [query]="{…}">42</app-kpi>`.
 * With a link the tile opens the list behind the number.
 */
@Component({
  selector: 'app-kpi',
  host: { class: 'block min-w-0' },
  imports: [NgTemplateOutlet, RouterLink],
  template: `
    <ng-template #body>
      <p class="stat-label">{{ label() }}</p>
      <p class="stat-value" dir="ltr"><ng-content /></p>
      @if (hint()) { <p class="mt-1 truncate text-xs text-muted">{{ hint() }}</p> }
    </ng-template>
    @if (link()) {
      <a [routerLink]="link()" [queryParams]="query()"
        class="stat-tile h-full text-start transition-[translate,box-shadow,border-color] duration-200 hover:-translate-y-0.5 hover:border-brand/25 hover:shadow-card-hover">
        <ng-container *ngTemplateOutlet="body" />
      </a>
    } @else {
      <div class="stat-tile h-full"><ng-container *ngTemplateOutlet="body" /></div>
    }
  `,
})
export class KpiTile {
  readonly label = input.required<string>();
  readonly hint = input<string | null>(null);
  readonly link = input<unknown[] | string | null>(null);
  readonly query = input<Params | null>(null);
}
