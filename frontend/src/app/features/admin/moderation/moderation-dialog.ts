import { Component, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Modal } from '../../../shared/ui/modal';

export type ModerationAction = 'disable' | 'enable' | 'delete';

/**
 * Confirms a moderation action. Disable and delete need a reason (shown to the owner, audited);
 * enable needs none. `title` is the already translated question.
 */
@Component({
  selector: 'app-moderation-dialog',
  imports: [TranslocoPipe, Modal],
  template: `
    <app-modal [label]="'admin.confirm.title' | transloco" (close)="!busy() && close.emit()">
      <h2 class="section-title">{{ title() }}</h2>
      <p class="mt-2 text-sm text-muted">{{ 'moderation.hints.' + action() | transloco }}</p>
      @if (action() !== 'enable') {
        <label for="moderation-reason" class="mt-5 field-label">{{ 'moderation.reasonLabel' | transloco }}</label>
        <textarea id="moderation-reason" rows="3" maxlength="500" [value]="reason()" (input)="reason.set($any($event.target).value)"
          [placeholder]="'moderation.reasonPlaceholder' | transloco"
          class="mt-1.5 field resize-y"></textarea>
      }
      @if (error(); as key) {
        <p role="alert" class="mt-4 alert-danger">{{ key | transloco }}</p>
      }
      <div class="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
        <button type="button" (click)="close.emit()" [disabled]="busy()" class="btn btn-ghost">
          {{ 'common.cancel' | transloco }}
        </button>
        <button type="button" (click)="confirm.emit(reason().trim())" [disabled]="busy() || (action() !== 'enable' && !reason().trim())"
          class="btn"
          [class]="action() === 'enable' ? 'btn-primary' : 'btn-danger'">
          {{ (busy() ? 'common.saving' : 'moderation.do.' + action()) | transloco }}
        </button>
      </div>
    </app-modal>
  `,
})
export class ModerationDialog {
  readonly action = input.required<ModerationAction>();
  readonly title = input.required<string>();
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly confirm = output<string>();
  readonly close = output<void>();

  protected readonly reason = signal('');
}
