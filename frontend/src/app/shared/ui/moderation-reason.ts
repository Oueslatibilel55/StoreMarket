import { Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { OWNER_ROLE_REVOKED } from '../../core/stores/store-api';

/** A moderation reason: typed by an admin (shown as is), or the automatic "owner role revoked" (translated). */
@Component({
  selector: 'app-moderation-reason',
  imports: [TranslocoPipe],
  template: `{{ reason() === revoked ? ('moderation.ownerRoleRevoked' | transloco) : reason() }}`,
})
export class ModerationReason {
  readonly reason = input<string | null>(null);
  protected readonly revoked = OWNER_ROLE_REVOKED;
}
