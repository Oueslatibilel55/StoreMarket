import { Component, input, output } from '@angular/core';

/** Accessible overlay dialog (no browser alert/confirm). Content goes in the default slot. */
@Component({
  selector: 'app-modal',
  host: { '(document:keydown.escape)': 'close.emit()' },
  template: `
    <div class="fixed inset-0 z-50 flex animate-fade-in items-end justify-center bg-ink/30 p-4 backdrop-blur-sm sm:items-center" (click)="close.emit()">
      <div role="dialog" aria-modal="true" [attr.aria-label]="label()"
        class="max-h-[90dvh] w-full max-w-md animate-pop-in overflow-y-auto rounded-xl border border-line bg-card p-6 shadow-pop" (click)="$event.stopPropagation()">
        <ng-content />
      </div>
    </div>
  `,
})
export class Modal {
  readonly label = input<string>('');
  readonly close = output<void>();
}
