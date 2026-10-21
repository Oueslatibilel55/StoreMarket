import { Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/** Coloured state pill of a store (`myStore.status.*`) or a product (`products.status.*`) in the moderation screens. */
@Component({
  selector: 'app-state-badge',
  imports: [TranslocoPipe],
  template: `<span class="pill" [class]="tone()">{{ key() | transloco }}</span>`,
})
export class StateBadge {
  /** Active, Closed, Disabled, Suspended, Deleted (stores) — Published, Draft, Archived, Disabled, Deleted (products). */
  readonly state = input.required<string>();
  readonly kind = input<'store' | 'product'>('store');

  protected readonly key = computed(() => `${this.kind() === 'store' ? 'myStore' : 'products'}.status.${this.state()}`);
  protected readonly tone = computed(() => {
    switch (this.state()) {
      case 'Active':
      case 'Published':
        return 'pill-success';
      case 'Closed':
      case 'Draft':
        return 'pill-warning';
      case 'Archived':
        return 'pill-neutral';
      default:
        return 'pill-danger';
    }
  });
}
