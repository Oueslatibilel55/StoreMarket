import { Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Role } from '../../core/auth/auth.models';

const STYLES: Record<Role, string> = {
  Buyer: 'pill-neutral',
  // Affiliates earn coins: olive, the colour of money earned.
  Affiliate: 'pill-success',
  StoreOwner: 'pill-brand',
  Admin: 'bg-ink text-white ring-ink',
};

@Component({
  selector: 'app-role-badge',
  imports: [TranslocoPipe],
  template: `<span class="pill" [class]="style()">{{ 'roles.' + role() | transloco }}</span>`,
})
export class RoleBadge {
  readonly role = input.required<Role>();
  protected style = () => STYLES[this.role()];
}
