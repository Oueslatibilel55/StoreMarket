import { Component, input, output } from '@angular/core';

/** Accessible overlay dialog (no browser alert/confirm). Content goes in the default slot. */
@Component({
  selector: 'app-modal',
  host: { '(document:keydown.escape)': 'close.emit()' },
  template: `
    <div class="fixed inset-0 z-50 flex animate-fade-in items-end justify-center bg-ink/50 sm:items-center sm:p-4" (click)="close.emit()">
      <div role="dialog" aria-modal="true" [attr.aria-label]="label()"
        class="max-h-[90dvh] w-full max-w-md animate-pop-in overflow-y-auto rounded-t-xl border border-line bg-card p-5 shadow-pop sm:rounded-xl sm:p-6 [&>h2:first-child]:font-display [&>h2:first-child]:text-xl [&>h2:first-child]:font-bold [&>h2:first-child]:tracking-tight [&>h2:first-child]:text-ink"
        (click)="$event.stopPropagation()">
        <ng-content />
      </div>
    </div>
  `,
})
export class Modal {
  readonly label = input<string>('');
  readonly close = output<void>();
}
